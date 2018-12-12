using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Newtonsoft.Json;
using Xunit;

namespace LaunchDarkly.Client.DynamoDB.Tests
{
    public class DynamoDBFeatureStoreTest : IDisposable
    {
        internal class TestData : IVersionedData
        {
            [JsonProperty(PropertyName = "key")]
            public string Key { get; set; }
            [JsonProperty(PropertyName = "version")]
            public int Version { get; set; }
            [JsonProperty(PropertyName = "deleted")]
            public bool Deleted { get; set; }
            [JsonProperty(PropertyName = "value")]
            internal string Value { get; set; }
        }

        class TestDataKind : VersionedDataKind<TestData>
        {
            public override string GetNamespace()
            {
                return "test";
            }

            public override TestData MakeDeletedItem(string key, int version)
            {
                return new TestData { Key = key, Version = version, Deleted = true };
            }

            public override Type GetItemType()
            {
                return typeof(TestData);
            }

            public override string GetStreamApiPath()
            {
                throw new NotImplementedException();
            }
        }

        private static readonly TestDataKind TestKind = new TestDataKind();
        private static bool TableCreated = false;
        private static readonly TaskFactory _taskFactory = new TaskFactory(CancellationToken.None,
            TaskCreationOptions.None, TaskContinuationOptions.None, TaskScheduler.Default);

        const string TableName = "test-dynamodb-table";
        const string Prefix = "test-prefix";

        private readonly IFeatureStore store;

        private readonly TestData item1 = new TestData { Key = "foo", Value = "first", Version = 10 };
        private readonly TestData item2 = new TestData { Key = "bar", Value = "second", Version = 10 };

        public DynamoDBFeatureStoreTest()
        {
            store = DatabaseComponents.DynamoDBFeatureStore(TableName)
                .WithPrefix(Prefix)
                .WithCredentials(MakeTestCredentials())
                .WithConfiguration(MakeTestConfiguration())
                .WithCaching(FeatureStoreCaching.Disabled)
                .CreateFeatureStore();
            CreateTableIfNecessary();
        }

        public void Dispose()
        {
            store.Dispose();
        }
        
        private AWSCredentials MakeTestCredentials()
        {
            return new BasicAWSCredentials("key", "secret"); // not used, but required
        }

        private AmazonDynamoDBConfig MakeTestConfiguration()
        {
            return new AmazonDynamoDBConfig()
            {
                ServiceURL = "http://localhost:8000"//,   // assumes we're running a local DynamoDB
                //RegionEndpoint = RegionEndpoint.USEast1 // not used, but required
            };
        }

        private void InitStore(IFeatureStore s)
        {
            IDictionary<string, IVersionedData> items = new Dictionary<string, IVersionedData>();
            items[item1.Key] = item1;
            items[item2.Key] = item2;
            IDictionary<IVersionedDataKind, IDictionary<string, IVersionedData>> allData =
                new Dictionary<IVersionedDataKind, IDictionary<string, IVersionedData>>();
            allData[TestKind] = items;
            s.Init(allData);
        }

        [Fact]
        public void StoreNotInitializedBeforeInit()
        {
            ClearAllData();
            Assert.False(store.Initialized());
        }

        [Fact]
        public void StoreInitializedAfterInit()
        {
            ClearAllData();
            InitStore(store);
            Assert.True(store.Initialized());
        }

        [Fact]
        public void GetExistingItem()
        {
            InitStore(store);
            var result = store.Get(TestKind, item1.Key);
        }
        
        [Fact]
        public void GetNonexistingItem()
        {
            InitStore(store);
            var result = store.Get(TestKind, "biz");
            Assert.Null(result);
        }
        
        [Fact]
        public void GetAllItems()
        {
            InitStore(store);
            var result = store.All(TestKind);
            Assert.Equal(2, result.Count);
            Assert.Equal(item1.Key, result[item1.Key].Key);
            Assert.Equal(item2.Key, result[item2.Key].Key);
        }

        [Fact]
        public void UpsertWithNewerVersion()
        {
            InitStore(store);
            var newVer = new TestData { Key = item1.Key, Version = item1.Version + 1, Value = "new" };
            store.Upsert(TestKind, newVer);
            var result = store.Get(TestKind, item1.Key);
            Assert.Equal(newVer.Value, result.Value);
        }

        [Fact]
        public void UpsertWithSameVersion()
        {
            InitStore(store);
            var newVer = new TestData { Key = item1.Key, Version = item1.Version, Value = "new" };
            store.Upsert(TestKind, newVer);
            var result = store.Get(TestKind, item1.Key);
            Assert.Equal(item1.Value, result.Value);
        }

        [Fact]
        public void UpsertWithOlderVersion()
        {
            InitStore(store);
            var newVer = new TestData { Key = item1.Key, Version = item1.Version - 1, Value = "new" };
            store.Upsert(TestKind, newVer);
            var result = store.Get(TestKind, item1.Key);
            Assert.Equal(item1.Value, result.Value);
        }

        [Fact]
        public void UpsertNewItem()
        {
            InitStore(store);
            var newItem = new TestData { Key = "biz", Version = 99 };
            store.Upsert(TestKind, newItem);
            var result = store.Get(TestKind, newItem.Key);
            Assert.Equal(newItem.Key, result.Key);
        }

        [Fact]
        public void DeleteWithNewerVersion()
        {
            InitStore(store);
            store.Delete(TestKind, item1.Key, item1.Version + 1);
            Assert.Null(store.Get(TestKind, item1.Key));
        }

        [Fact]
        public void DeleteWithSameVersion()
        {
            InitStore(store);
            store.Delete(TestKind, item1.Key, item1.Version);
            Assert.NotNull(store.Get(TestKind, item1.Key));
        }

        [Fact]
        public void DeleteWithOlderVersion()
        {
            InitStore(store);
            store.Delete(TestKind, item1.Key, item1.Version - 1);
            Assert.NotNull(store.Get(TestKind, item1.Key));
        }

        [Fact]
        public void DeleteUnknownItem()
        {
            InitStore(store);
            store.Delete(TestKind, "biz", 11);
            Assert.Null(store.Get(TestKind, "biz"));
        }

        [Fact]
        public void UpsertOlderVersionAfterDelete()
        {
            InitStore(store);
            store.Delete(TestKind, item1.Key, item1.Version + 1);
            store.Upsert(TestKind, item1);
            Assert.Null(store.Get(TestKind, item1.Key));
        }

        private void CreateTableIfNecessary()
        {
            if (TableCreated)
            {
                return;
            }

            using (var client = CreateTestClient())
            {
                WaitSafely(async () =>
                {
                    try
                    {
                        await client.DescribeTableAsync(new DescribeTableRequest(TableName));
                        return; // table exists
                    }
                    catch (ResourceNotFoundException)
                    {
                        // fall through to code below - we'll create the table
                    }
                    var request = new CreateTableRequest()
                    {
                        TableName = TableName,
                        KeySchema = new List<KeySchemaElement>()
                        {
                            new KeySchemaElement(Constants.PartitionKey, KeyType.HASH),
                            new KeySchemaElement(Constants.SortKey, KeyType.RANGE)
                        },
                        AttributeDefinitions = new List<AttributeDefinition>()
                        {
                            new AttributeDefinition(Constants.PartitionKey, ScalarAttributeType.S),
                            new AttributeDefinition(Constants.SortKey, ScalarAttributeType.S)
                        },
                        ProvisionedThroughput = new ProvisionedThroughput(1, 1)
                    };
                    await client.CreateTableAsync(request);
                });
            }

            TableCreated = true;
        }

        private void ClearAllData()
        {
            using (var client = CreateTestClient())
            {
                WaitSafely(async () =>
                {
                    var deleteReqs = new List<WriteRequest>();
                    ScanRequest request = new ScanRequest(TableName)
                    {
                        ConsistentRead = true,
                        ProjectionExpression = "#namespace, #key",
                        ExpressionAttributeNames = new Dictionary<string, string>()
                        {
                            { "#namespace", Constants.PartitionKey },
                            { "#key", Constants.SortKey }
                        }
                    };
                    await DynamoDBHelpers.IterateScan(client, request,
                        item => deleteReqs.Add(new WriteRequest(new DeleteRequest(item))));
                    await DynamoDBHelpers.BatchWriteRequestsAsync(client, TableName, deleteReqs);
                });
            }
        }

        private AmazonDynamoDBClient CreateTestClient()
        {
            return new AmazonDynamoDBClient(MakeTestCredentials(), MakeTestConfiguration());
        }
        
        private void WaitSafely(Func<Task> taskFn)
        {
            _taskFactory.StartNew(taskFn)
                .Unwrap()
                .GetAwaiter()
                .GetResult();
        }
    }
}
