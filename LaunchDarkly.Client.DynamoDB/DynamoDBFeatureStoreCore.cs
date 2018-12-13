using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Common.Logging;
using LaunchDarkly.Client.Utils;

namespace LaunchDarkly.Client.DynamoDB
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
    internal sealed class DynamoDBFeatureStoreCore : IFeatureStoreCoreAsync
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(DynamoDBFeatureStoreCore));

        // These attribute names aren't public because application code should never access them directly
        private const string VersionAttribute = "version";
        private const string ItemJsonAttribute = "item";

        private readonly AmazonDynamoDBClient _client;
        private readonly string _tableName;
        private readonly string _prefix;
        
        internal DynamoDBFeatureStoreCore(AmazonDynamoDBClient client, string tableName, string prefix)
        {
            Log.InfoFormat("Creating DynamoDB feature store with table name \"{0}\"", tableName);

            _client = client;
            _tableName = tableName;
            _prefix = (prefix == "") ? null : _prefix;
        }
        
        public async Task<bool> InitializedInternalAsync()
        {
            var resp = await GetItemByKeys(InitedKey, InitedKey);
            return resp.Item != null && resp.Item.Count > 0;
        }

        public async Task InitInternalAsync(IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> allData)
        {
            // Start by reading the existing keys; we will later delete any of these that weren't in allData.
            var unusedOldKeys = await ReadExistingKeys(allData.Keys);

            var requests = new List<WriteRequest>();
            var numItems = 0;

            // Insert or update every provided item
            foreach (var entry in allData)
            {
                var kind = entry.Key;
                foreach (var item in entry.Value.Values)
                {
                    var encodedItem = MarshalItem(kind, item);
                    requests.Add(new WriteRequest(new PutRequest(encodedItem)));

                    var combinedKey = new Tuple<string, string>(NamespaceForKind(kind), item.Key);
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

            Log.InfoFormat("Initialized table {0} with {1} items", _tableName, numItems);
        }

        public async Task<IVersionedData> GetInternalAsync(IVersionedDataKind kind, String key)
        {
            var resp = await GetItemByKeys(NamespaceForKind(kind), key);
            return UnmarshalItem(kind, resp.Item);
        }
        
        public async Task<IDictionary<string, IVersionedData>> GetAllInternalAsync(IVersionedDataKind kind)
        {
            var ret = new Dictionary<string, IVersionedData>();
            var req = MakeQueryForKind(kind);
            await DynamoDBHelpers.IterateQuery(_client, req,
                item =>
                {
                    var itemOut = UnmarshalItem(kind, item);
                    if (itemOut != null)
                    {
                        ret[itemOut.Key] = itemOut;
                    }
                });
            return ret;
        }

        public async Task<IVersionedData> UpsertInternalAsync(IVersionedDataKind kind, IVersionedData item)
        {
            var encodedItem = MarshalItem(kind, item);
            
            try
            {
                var request = new PutItemRequest(_tableName, encodedItem);
                request.ConditionExpression = "attribute_not_exists(#namespace) or attribute_not_exists(#key) or :version > #version";
                request.ExpressionAttributeNames = new Dictionary<string, string>()
                {
                    { "#namespace", Constants.PartitionKey },
                    { "#key", Constants.SortKey },
                    { "#version", VersionAttribute }
                };
                request.ExpressionAttributeValues = new Dictionary<string, AttributeValue>()
                {
                    { ":version", new AttributeValue() { N = Convert.ToString(item.Version) } }
                };
                await _client.PutItemAsync(request);
            }
            catch (ConditionalCheckFailedException)
            {
                // The item was not updated because there's a newer item in the database.
                // We must now read the item that's in the database and return it, so CachingStoreWrapper can cache it.
                return await GetInternalAsync(kind, item.Key);
            }

            return item;
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
                _client.Dispose();
            }
        }

        private string PrefixedNamespace(string baseStr)
        {
            return _prefix == null ? baseStr : (_prefix + ":" + baseStr);
        }

        private string NamespaceForKind(IVersionedDataKind kind)
        {
            return PrefixedNamespace(kind.GetNamespace());
        }

        private string InitedKey
        {
            get
            {
                return PrefixedNamespace("$inited");
            }
        }

        private Dictionary<string, AttributeValue> MakeKeysMap(string ns, string key)
        {
            return new Dictionary<string, AttributeValue>()
            {
                { Constants.PartitionKey, new AttributeValue(ns) },
                { Constants.SortKey, new AttributeValue(key) }
            };
        }

        private QueryRequest MakeQueryForKind(IVersionedDataKind kind)
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
                    { Constants.PartitionKey, cond }
                },
                ConsistentRead = true
            };
        }

        private Task<GetItemResponse> GetItemByKeys(string ns, string key)
        {
            var req = new GetItemRequest(_tableName, MakeKeysMap(ns, key), true);
            return _client.GetItemAsync(req);
        }

        private async Task<HashSet<Tuple<string, string>>> ReadExistingKeys(ICollection<IVersionedDataKind> kinds)
        {
            var keys = new HashSet<Tuple<string, string>>();
            foreach (var kind in kinds)
            {
                var req = MakeQueryForKind(kind);
                req.ProjectionExpression = "#namespace, #key";
                req.ExpressionAttributeNames = new Dictionary<string, string>()
                {
                    { "#namespace", Constants.PartitionKey },
                    { "#key", Constants.SortKey }
                };
                await DynamoDBHelpers.IterateQuery(_client, req,
                    item => keys.Add(new Tuple<string, string>(
                        item[Constants.PartitionKey].S,
                        item[Constants.SortKey].S))
                    );
            }
            return keys;
        }

        private Dictionary<string, AttributeValue> MarshalItem(IVersionedDataKind kind, IVersionedData item)
        {
            var json = FeatureStoreHelpers.MarshalJson(item);
            var ret = MakeKeysMap(NamespaceForKind(kind), item.Key);
            ret[VersionAttribute] = new AttributeValue() { N = item.Version.ToString() };
            ret[ItemJsonAttribute] = new AttributeValue(json);
            return ret;
        }

        private IVersionedData UnmarshalItem(IVersionedDataKind kind, IDictionary<string, AttributeValue> item)
        {
            if (item == null || item.Count == 0)
            {
                return null;
            }
            if (item.TryGetValue(ItemJsonAttribute, out var jsonAttr) && jsonAttr.S != null)
            {
                return FeatureStoreHelpers.UnmarshalJson(kind, jsonAttr.S);
            }
            throw new InvalidOperationException("DynamoDB map did not contain expected item string");
        }
    }
}
