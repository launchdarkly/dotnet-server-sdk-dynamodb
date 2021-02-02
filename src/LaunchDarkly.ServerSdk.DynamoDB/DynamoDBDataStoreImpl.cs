﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Interfaces;

using static LaunchDarkly.Sdk.Server.Interfaces.DataStoreTypes;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    /// <summary>
    /// Internal implementation of the DynamoDB feature store.
    /// 
    /// Implementation notes:
    /// 
    /// * The AWS SDK methods are asynchronous; currently none of the LaunchDarkly SDK code is
    /// asynchronous. Therefore, this implementation is async and we're relying on an adapter
    /// that is part of CachingStoreWrapper to allow us to be called from synchronous code. If
    /// our SDK is changed to use async code in the future, we should not have to change anything
    /// in this class.
    /// 
    /// * Feature flags, segments, and any other kind of entity the LaunchDarkly client may wish
    /// to store, are all put in the same table. The only two required attributes are "key" (which
    /// is present in all storeable entities) and "namespace" (a parameter from the client that is
    /// used to disambiguate between flags and segments).
    /// 
    /// * Because of DynamoDB's restrictions on attribute values (e.g. empty strings are not
    /// allowed), the standard DynamoDB marshaling mechanism with one attribute per object property
    /// is not used. Instead, the entire object is serialized to JSON and stored in a single
    /// attribute, "item". The "version" property is also stored as a separate attribute since it
    /// is used for updates.
    /// 
    /// * Since DynamoDB doesn't have transactions, the Init method - which replaces the entire data
    /// store - is not atomic, so there can be a race condition if another process is adding new data
    /// via Upsert. To minimize this, we don't delete all the data at the start; instead, we update
    /// the items we've received, and then delete all other items. That could potentially result in
    /// deleting new data from another process, but that would be the case anyway if the Init
    /// happened to execute later than the upsert(); we are relying on the fact that normally the
    /// process that did the init() will also receive the new data shortly and do its own Upsert.
    /// 
    /// * DynamoDB has a maximum item size of 400KB. Since each feature flag or user segment is
    /// stored as a single item, this mechanism will not work for extremely large flags or segments.
    /// </summary>
    internal sealed class DynamoDBDataStoreImpl : IPersistentDataStoreAsync
    {
        // These attribute names aren't public because application code should never access them directly
        private const string VersionAttribute = "version";
        private const string SerializedItemAttribute = "item";
        private const string DeletedItemPlaceholder = "null"; // DynamoDB does not allow empty strings

        private readonly AmazonDynamoDBClient _client;
        private readonly bool _wasExistingClient;
        private readonly string _tableName;
        private readonly string _prefix;
        private readonly Logger _log;

        internal DynamoDBDataStoreImpl(
            AmazonDynamoDBClient client,
            bool wasExistingClient,
            string tableName,
            string prefix,
            Logger log
            )
        {
            _client = client;
            _wasExistingClient = wasExistingClient;
            _tableName = tableName;
            _log = log;

            if (string.IsNullOrEmpty(prefix))
            {
                _prefix = null;
                _log.Info("Using DynamoDB data store with table name \"{0}\" and no prefix", tableName);
            }
            else
            {
                _log.Info("Using DynamoDB data store with table name \"{0}\" and prefix \"{1}\"",
                    tableName, prefix);
                _prefix = prefix;
            }
        }
        
        public async Task<bool> InitializedAsync()
        {
            var resp = await GetItemByKeys(InitedKey, InitedKey);
            return resp.Item != null && resp.Item.Count > 0;
        }

        public async Task InitAsync(FullDataSet<SerializedItemDescriptor> allData)
        {
            // Start by reading the existing keys; we will later delete any of these that weren't in allData.
            var unusedOldKeys = await ReadExistingKeys(allData.Data.Select(collection => collection.Key));

            var requests = new List<WriteRequest>();
            var numItems = 0;

            // Insert or update every provided item
            foreach (var collection in allData.Data)
            {
                var kind = collection.Key;
                foreach (var keyAndItem in collection.Value.Items)
                {
                    var encodedItem = MarshalItem(kind, keyAndItem.Key, keyAndItem.Value);
                    requests.Add(new WriteRequest(new PutRequest(encodedItem)));

                    var combinedKey = new Tuple<string, string>(NamespaceForKind(kind), keyAndItem.Key);
                    unusedOldKeys.Remove(combinedKey);

                    numItems++;
                }
            }

            // Now delete any previously existing items whose keys were not in the current data
            foreach (var combinedKey in unusedOldKeys)
            {
                if (combinedKey.Item1 != InitedKey)
                {
                    var keys = MakeKeysMap(combinedKey.Item1, combinedKey.Item2);
                    requests.Add(new WriteRequest(new DeleteRequest(keys)));
                }
            }

            // Now set the special key that we check in initializedInternal()
            var initedItem = MakeKeysMap(InitedKey, InitedKey);
            requests.Add(new WriteRequest(new PutRequest(initedItem)));

            await DynamoDBHelpers.BatchWriteRequestsAsync(_client, _tableName, requests);

            _log.Info("Initialized data store with {0} items", numItems);
        }

        public async Task<SerializedItemDescriptor?> GetAsync(DataKind kind, String key)
        {
            var resp = await GetItemByKeys(NamespaceForKind(kind), key);
            return UnmarshalItem(kind, resp.Item);
        }
        
        public async Task<KeyedItems<SerializedItemDescriptor>> GetAllAsync(DataKind kind)
        {
            var ret = new List<KeyValuePair<string, SerializedItemDescriptor>>();
            var req = MakeQueryForKind(kind);
            await DynamoDBHelpers.IterateQuery(_client, req,
                item =>
                {
                    var itemOut = UnmarshalItem(kind, item);
                    if (itemOut.HasValue)
                    {
                        var itemKey = item[DynamoDB.DataStoreSortKey].S;
                        ret.Add(new KeyValuePair<string, SerializedItemDescriptor>(itemKey, itemOut.Value));
                    }
                });
            return new KeyedItems<SerializedItemDescriptor>(ret);
        }

        public async Task<bool> UpsertAsync(DataKind kind, string key, SerializedItemDescriptor newItem)
        {
            var encodedItem = MarshalItem(kind, key, newItem);
            
            try
            {
                var request = new PutItemRequest(_tableName, encodedItem);
                request.ConditionExpression = "attribute_not_exists(#namespace) or attribute_not_exists(#key) or :version > #version";
                request.ExpressionAttributeNames = new Dictionary<string, string>()
                {
                    { "#namespace", DynamoDB.DataStorePartitionKey },
                    { "#key", DynamoDB.DataStoreSortKey },
                    { "#version", VersionAttribute }
                };
                request.ExpressionAttributeValues = new Dictionary<string, AttributeValue>()
                {
                    { ":version", new AttributeValue() { N = Convert.ToString(newItem.Version) } }
                };
                await _client.PutItemAsync(request);
            }
            catch (ConditionalCheckFailedException)
            {
                return false;
            }

            return true;
        }

        public async Task<bool> IsStoreAvailableAsync()
        {
            try
            {
                await InitializedAsync(); // don't care about the return value, just that it doesn't throw an exception
                return true;
            }
            catch
            { // don't care about exception class, since any exception means the DynamoDB request couldn't be made
                return false;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_wasExistingClient)
                {
                    _client.Dispose();
                }
            }
        }

        private string PrefixedNamespace(string baseStr) =>
            _prefix is null ? baseStr : (_prefix + ":" + baseStr);

        private string NamespaceForKind(DataKind kind) =>
            PrefixedNamespace(kind.Name);

        private string InitedKey => PrefixedNamespace("$inited");

        private Dictionary<string, AttributeValue> MakeKeysMap(string ns, string key) =>
            new Dictionary<string, AttributeValue>()
            {
                { DynamoDB.DataStorePartitionKey, new AttributeValue(ns) },
                { DynamoDB.DataStoreSortKey, new AttributeValue(key) }
            };

        private QueryRequest MakeQueryForKind(DataKind kind)
        {
            Condition cond = new Condition()
            {
                ComparisonOperator = ComparisonOperator.EQ,
                AttributeValueList = new List<AttributeValue>()
                {
                    new AttributeValue(NamespaceForKind(kind))
                }
            };
            return new QueryRequest(_tableName)
            {
                KeyConditions = new Dictionary<string, Condition>()
                {
                    { DynamoDB.DataStorePartitionKey, cond }
                },
                ConsistentRead = true
            };
        }

        private Task<GetItemResponse> GetItemByKeys(string ns, string key)
        {
            var req = new GetItemRequest(_tableName, MakeKeysMap(ns, key), true);
            return _client.GetItemAsync(req);
        }

        private async Task<HashSet<Tuple<string, string>>> ReadExistingKeys(IEnumerable<DataKind> kinds)
        {
            var keys = new HashSet<Tuple<string, string>>();
            foreach (var kind in kinds)
            {
                var req = MakeQueryForKind(kind);
                req.ProjectionExpression = "#namespace, #key";
                req.ExpressionAttributeNames = new Dictionary<string, string>()
                {
                    { "#namespace", DynamoDB.DataStorePartitionKey },
                    { "#key", DynamoDB.DataStoreSortKey }
                };
                await DynamoDBHelpers.IterateQuery(_client, req,
                    item => keys.Add(new Tuple<string, string>(
                        item[DynamoDB.DataStorePartitionKey].S,
                        item[DynamoDB.DataStoreSortKey].S))
                    );
            }
            return keys;
        }

        private Dictionary<string, AttributeValue> MarshalItem(DataKind kind, string key, SerializedItemDescriptor item)
        {
            var ret = MakeKeysMap(NamespaceForKind(kind), key);
            ret[VersionAttribute] = new AttributeValue() { N = item.Version.ToString() };
            ret[SerializedItemAttribute] = new AttributeValue(item.Deleted ? DeletedItemPlaceholder : item.SerializedItem);
            return ret;
        }

        private SerializedItemDescriptor? UnmarshalItem(DataKind kind, IDictionary<string, AttributeValue> item)
        {
            if (item is null || item.Count == 0)
            {
                return null;
            }
            if (!item.TryGetValue(SerializedItemAttribute, out var serializedItemAttr) || serializedItemAttr.S is null)
            {
                throw new InvalidOperationException("Invalid data in DynamoDB: missing item attribute");
            }
            if (!item.TryGetValue(VersionAttribute, out var versionAttr) || versionAttr.N is null)
            {
                throw new InvalidOperationException("Invalid data in DynamoDB: missing version attribute");
            }
            if (!int.TryParse(versionAttr.N, out var version))
            {
                throw new InvalidOperationException("Invalid data in DynamoDB: non-numeric version");
            }
            if (serializedItemAttr.S == DeletedItemPlaceholder)
            {
                return new SerializedItemDescriptor(version, true, null);
            }
            return new SerializedItemDescriptor(version, false, serializedItemAttr.S);
        }
    }
}
