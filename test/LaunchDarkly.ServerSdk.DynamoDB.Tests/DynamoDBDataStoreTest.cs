using System.Threading.Tasks;
using LaunchDarkly.Logging;
using LaunchDarkly.Sdk.Server.Interfaces;
using LaunchDarkly.Sdk.Server.SharedTests.DataStore;
using Xunit;
using Xunit.Abstractions;

using static LaunchDarkly.Sdk.Server.Integrations.DynamoDBTestEnvironment;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    public class DynamoDBDataStoreTest : PersistentDataStoreBaseTests, IAsyncLifetime
    {
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
