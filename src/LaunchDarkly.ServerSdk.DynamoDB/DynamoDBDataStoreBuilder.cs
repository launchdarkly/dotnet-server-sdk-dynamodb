using Amazon.DynamoDBv2;
using Amazon.Runtime;
using LaunchDarkly.Sdk.Server.Interfaces;

namespace LaunchDarkly.Sdk.Server.Integrations
{
    /// <summary>
    /// A builder for configuring the DynamoDB-based persistent data store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Obtain an instance of this class by calling <see cref="DynamoDB.DataStore(string)"/>. After calling its methods
    /// to specify any desired custom settings, wrap it in a <see cref="PersistentDataStoreBuilder"/>
    /// by calling <see cref="Components.PersistentDataStore(IPersistentDataStoreAsyncFactory)"/>, then pass
    /// the result into the SDK configuration with <see cref="ConfigurationBuilder.DataStore(IDataStoreFactory)"/>.
    /// You do not need to call <see cref="CreatePersistentDataStore(LdClientContext)"/> yourself to build
    /// the actual data store; that will be done by the SDK.
    /// </para>
    /// <para>
    /// The AWS SDK provides many configuration options for a DynamoDB client. This class has
    /// corresponding methods for some of the most commonly used ones. If you need more sophisticated
    /// control over the DynamoDB client, you can construct one of your own and pass it in with the
    /// <see cref="ExistingClient(AmazonDynamoDBClient)"/> method.
    /// </para>
    /// <para>
    /// Builder calls can be chained, for example:
    /// </para>
    /// <code>
    ///     var config = Configuration.Builder("sdk-key")
    ///         .DataStore(
    ///             Components.PersistentDataStore(
    ///                 DynamoDB.DataStore("my-table-name")
    ///                     .Credentials(myAWSCredentials)
    ///                     .Prefix("app1")
    ///                 )
    ///                 .CacheSeconds(15)
    ///             )
    ///         .Build();
    /// </code>
    /// </remarks>
    public sealed class DynamoDBDataStoreBuilder : IPersistentDataStoreAsyncFactory
    {
        private AmazonDynamoDBClient _existingClient = null;
        private AWSCredentials _credentials = null;
        private AmazonDynamoDBConfig _config = null;

        private readonly string _tableName;
        private string _prefix = "";
        
        internal DynamoDBDataStoreBuilder(string tableName)
        {
            _tableName = tableName;
        }

        /// <summary>
        /// Specifies an existing, already-configured DynamoDB client instance that the data store
        /// should use rather than creating one of its own.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If you specify an existing client, then the other builder methods for configuring DynamoDB
        /// are ignored.
        /// </para>
        /// <para>
        /// Note that the LaunchDarkly code will <i>not</i> take ownership of the lifecycle of this
        /// object: in other words, it will not call <c>Dispose()</c> on the <c>AmazonDynamoDBClient</c> when
        /// you dispose of the SDK client, as it would if it had created the <c>AmazonDynamoDBClient</c> itself.
        /// It is your responsibility to call <c>Dispose()</c> on the <c>AmazonDynamoDBClient</c> when you are
        /// done with it.
        /// </para>
        /// </remarks>
        /// <param name="client">an existing DynamoDB client instance</param>
        /// <returns>the builder</returns>
        public DynamoDBDataStoreBuilder ExistingClient(AmazonDynamoDBClient client)
        {
            _existingClient = client;
            return this;
        }

        /// <summary>
        /// Sets the AWS client credentials.
        /// </summary>
        /// <remarks>
        /// If you do not set them programmatically, the AWS SDK will attempt to find them in
        /// environment variables and/or local configuration files.
        /// </remarks>
        /// <param name="credentials">the AWS credentials</param>
        /// <returns>the builder</returns>
        public DynamoDBDataStoreBuilder Credentials(AWSCredentials credentials)
        {
            _credentials = credentials;
            return this;
        }

        /// <summary>
        /// Specifies an entire DynamoDB configuration.
        /// </summary>
        /// <remarks>
        /// If this is not provided explicitly, the AWS SDK will attempt to determine your
        /// current region based on environment variables and/or local configuration files.
        /// </remarks>
        /// <param name="config">a DynamoDB configuration object</param>
        /// <returns>the builder</returns>
        public DynamoDBDataStoreBuilder Configuration(AmazonDynamoDBConfig config)
        {
            _config = config;
            return this;
        }

        /// <summary>
        /// Sets an optional namespace prefix for all keys stored in DynamoDB.
        /// </summary>
        /// <remarks>
        /// You may use this if you are sharing the same database table between multiple clients that
        /// are for different LaunchDarkly environments, to avoid key collisions. However, in DynamoDB
        /// it is common to use separate tables rather than share a single table for unrelated
        /// applications, so by default there is no prefix.
        /// </remarks>
        /// <param name="prefix">the namespace prefix; null for no prefix</param>
        /// <returns>the builder</returns>
        public DynamoDBDataStoreBuilder Prefix(string prefix)
        {
            _prefix = prefix;
            return this;
        }

        /// <inheritdoc/>
        public IPersistentDataStoreAsync CreatePersistentDataStore(LdClientContext context) =>
            new DynamoDBDataStoreImpl(
                MakeClient(),
                _existingClient != null,
                _tableName,
                _prefix,
                context.Basic.Logger.SubLogger("DataStore.DynamoDB")
                );

        private AmazonDynamoDBClient MakeClient()
        {
            if (_existingClient != null)
            {
                return _existingClient;
            }
            // Unfortunately, the AWS SDK does not believe in builders
            if (_credentials == null)
            {
                if (_config == null)
                {
                    return new AmazonDynamoDBClient();
                }
                return new AmazonDynamoDBClient(_config);
            }
            if (_config == null)
            {
                return new AmazonDynamoDBClient(_credentials);
            }
            return new AmazonDynamoDBClient(_credentials, _config);
        }
    }
}
