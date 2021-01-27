using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using LaunchDarkly.Client.SharedTests.FeatureStore;

namespace LaunchDarkly.Client.DynamoDB.Tests
{
    public class DynamoDBFeatureStoreTest : FeatureStoreBaseTests
    {
        private static bool TableCreated = false;
        private static readonly TaskFactory _taskFactory = new TaskFactory(CancellationToken.None,
            TaskCreationOptions.None, TaskContinuationOptions.None, TaskScheduler.Default);

        const string TableName = "test-dynamodb-table";

        protected override IFeatureStore CreateStoreImpl(FeatureStoreCacheConfig caching)
        {
            CreateTableIfNecessary();
            return Components.PersistentDataStore(BaseBuilder())
                .CacheTime(caching.Ttl)
                .CreateFeatureStore();
        }
        
        protected override IFeatureStore CreateStoreImplWithPrefix(string prefix)
        {
            CreateTableIfNecessary();
            return Components.PersistentDataStore(BaseBuilder().Prefix(prefix))
                .NoCaching()
                .CreateFeatureStore();
        }

        private Integrations.DynamoDBDataStoreBuilder BaseBuilder()
        {
            return Integrations.DynamoDB.DataStore(TableName)
                .Credentials(MakeTestCredentials())
                .Configuration(MakeTestConfiguration());
        }

        private AWSCredentials MakeTestCredentials()
        {
            return new BasicAWSCredentials("key", "secret"); // not used, but required
        }

        private AmazonDynamoDBConfig MakeTestConfiguration()
        {
            return new AmazonDynamoDBConfig()
            {
                ServiceURL = "http://localhost:8000"   // assumes we're running a local DynamoDB
            };
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

        override protected void ClearAllData()
        {
            CreateTableIfNecessary();
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
