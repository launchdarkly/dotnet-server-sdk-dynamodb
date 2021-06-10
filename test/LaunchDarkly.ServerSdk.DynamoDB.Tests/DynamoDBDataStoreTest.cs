using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.SharedTests.DataStore;
using Xunit;
using Xunit.Abstractions;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    public class DynamoDBDataStoreTest : PersistentDataStoreBaseTests, IAsyncLifetime
    {
        private static bool TableCreated = false;

        const string TableName = "test-dynamodb-table";

        public DynamoDBDataStoreTest(ITestOutputHelper testOutput) : base(testOutput) { }

        protected override PersistentDataStoreTestConfig Configuration =>
            new PersistentDataStoreTestConfig
            {
                StoreAsyncFactoryFunc = MakeStoreFactory,
                ClearDataAction = ClearAllData
            };


        public Task InitializeAsync() => CreateTableIfNecessary();

        public Task DisposeAsync() => Task.CompletedTask;

        private IPersistentDataStoreAsyncFactory MakeStoreFactory(string prefix) =>
            BaseBuilder().Prefix(prefix);

        private DynamoDBDataStoreBuilder BaseBuilder() =>
            DynamoDB.DataStore(TableName)
                .Credentials(MakeTestCredentials())
                .Configuration(MakeTestConfiguration());

        private AWSCredentials MakeTestCredentials() =>
            new BasicAWSCredentials("key", "secret"); // not used, but required
        
        private AmazonDynamoDBConfig MakeTestConfiguration() =>
            new AmazonDynamoDBConfig()
            {
                ServiceURL = "http://localhost:8000"   // assumes we're running a local DynamoDB
            };
        
        private async Task CreateTableIfNecessary()
        {
            if (TableCreated)
            {
                return;
            }

            using (var client = CreateTestClient())
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
                        new KeySchemaElement(DynamoDB.DataStorePartitionKey, KeyType.HASH),
                        new KeySchemaElement(DynamoDB.DataStoreSortKey, KeyType.RANGE)
                    },
                    AttributeDefinitions = new List<AttributeDefinition>()
                    {
                        new AttributeDefinition(DynamoDB.DataStorePartitionKey, ScalarAttributeType.S),
                        new AttributeDefinition(DynamoDB.DataStoreSortKey, ScalarAttributeType.S)
                    },
                    ProvisionedThroughput = new ProvisionedThroughput(1, 1)
                };
                await client.CreateTableAsync(request);
            }

            TableCreated = true;
        }

        private async Task ClearAllData(string prefix)
        {
            var keyPrefix = prefix is null ? "" : (prefix + ":");
            using (var client = CreateTestClient())
            {
                var deleteReqs = new List<WriteRequest>();
                ScanRequest request = new ScanRequest(TableName)
                {
                    ConsistentRead = true,
                    ProjectionExpression = "#namespace, #key",
                    ExpressionAttributeNames = new Dictionary<string, string>()
                    {
                        { "#namespace", DynamoDB.DataStorePartitionKey },
                        { "#key", DynamoDB.DataStoreSortKey }
                    }
                };
                await DynamoDBHelpers.IterateScan(client, request,
                    item =>
                    {
                        if (item[DynamoDB.DataStorePartitionKey].S.StartsWith(keyPrefix))
                        {
                            deleteReqs.Add(new WriteRequest(new DeleteRequest(item)));
                        }
                    });
                await DynamoDBHelpers.BatchWriteRequestsAsync(client, TableName, deleteReqs);
            }
        }

        private AmazonDynamoDBClient CreateTestClient() =>
            new AmazonDynamoDBClient(MakeTestCredentials(), MakeTestConfiguration());

        [Fact]
        public void LogMessageAtStartup()
        {
            var logCapture = Logs.Capture();
            var logger = logCapture.Logger("BaseLoggerName"); // in real life, the SDK will provide its own base log name
            var context = new LdClientContext(new BasicConfiguration("", false, logger),
                LaunchDarkly.Sdk.Server.Configuration.Default(""));
            using (BaseBuilder().Prefix("my-prefix").CreatePersistentDataStore(context))
            {
                Assert.Collection(logCapture.GetMessages(),
                    m =>
                    {
                        Assert.Equal(LaunchDarkly.Logging.LogLevel.Info, m.Level);
                        Assert.Equal("BaseLoggerName.DataStore.DynamoDB", m.LoggerName);
                        Assert.Equal("Using DynamoDB data store with table name \"" + TableName +
                            "\" and prefix \"my-prefix\"", m.Text);
                    });
            }
        }
    }
}
