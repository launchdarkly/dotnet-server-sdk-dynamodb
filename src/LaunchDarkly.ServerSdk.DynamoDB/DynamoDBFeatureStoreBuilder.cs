using Amazon.DynamoDBv2;
using Amazon.Runtime;
using LaunchDarkly.Client.Utils;

namespace LaunchDarkly.Client.DynamoDB
{
    /// <summary>
    /// Obsolete builder for the DynamoDB data store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is retained in version 1.1 of the library for backward compatibility. For the new
    /// preferred way to configure the DynamoDB integration, see <see cref="LaunchDarkly.Client.Integrations.DynamoDB"/>.
    /// Updating to the latter now will make it easier to adopt version 6.0 of the LaunchDarkly .NET SDK, since
    /// an identical API is used there (except for the base namespace).
    /// </para>
    /// </remarks>
    [Obsolete("Use LaunchDarkly.Client.Integrations.DynamoDB")]
    public sealed class DynamoDBFeatureStoreBuilder : IFeatureStoreFactory
    {
        private AmazonDynamoDBClient _existingClient = null;
        private AWSCredentials _credentials = null;
        private AmazonDynamoDBConfig _config = null;

        private readonly string _tableName;
        private string _prefix = "";
        private FeatureStoreCacheConfig _caching = FeatureStoreCacheConfig.Enabled;
        
        /// <summary>
        /// Creates a new <see cref="DynamoDBFeatureStoreBuilder"/> with default properties.
        /// </summary>
        /// <returns>a builder</returns>
        internal DynamoDBFeatureStoreBuilder(string tableName)
        {
            this._tableName = tableName;
        }

        /// <summary>
        /// Creates a feature store instance based on the currently configured builder.
        /// </summary>
        /// <returns>the feature store</returns>
        public IFeatureStore CreateFeatureStore()
        {
            var core = new DynamoDBFeatureStoreCore(MakeClient(), _tableName, _prefix);
            return CachingStoreWrapper.Builder(core).WithCaching(_caching).Build();
        }

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
                else
                {
                    return new AmazonDynamoDBClient(_config);
                }
            }
            else
            {
                if (_config == null)
                {
                    return new AmazonDynamoDBClient(_credentials);
                }
                else
                {
                    return new AmazonDynamoDBClient(_credentials, _config);
                }
            }
        }

        /// <summary>
        /// Specifies an existing, already-configured DynamoDB client instance that the feature store
        /// should use rather than creating one of its own. If you specify an existing client, then the
        /// other builder methods for configuring DynamoDB are ignored.
        /// </summary>
        /// <param name="client">an existing DynamoDB client instance</param>
        /// <returns>the builder</returns>
        public DynamoDBFeatureStoreBuilder WithExistingClient(AmazonDynamoDBClient client)
        {
            _existingClient = client;
            return this;
        }

        /// <summary>
        /// Specifies the AWS credentials. If they are not provided explicitly, the AWS SDK
        /// will attempt to find them in environment variables and/or local configuration files.
        /// </summary>
        /// <param name="credentials">the AWS credentials</param>
        /// <returns>the builder</returns>
        public DynamoDBFeatureStoreBuilder WithCredentials(AWSCredentials credentials)
        {
            _credentials = credentials;
            return this;
        }

        /// <summary>
        /// Specifies an entire DynamoDB configuration. If this is not provided explicitly, the AWS
        /// SDK will attempt to determine your current region based on environment variables and/or
        /// local configuration files.
        /// </summary>
        /// <param name="config">a DynamoDB configuration object</param>
        /// <returns>the builder</returns>
        public DynamoDBFeatureStoreBuilder WithConfiguration(AmazonDynamoDBConfig config)
        {
            _config = config;
            return this;
        }

        /// <summary>
        /// Specifies whether local caching should be enabled and if so, sets the cache properties. Local
        /// caching is enabled by default; see <see cref="FeatureStoreCacheConfig.Enabled"/>. To disable it, pass
        /// <see cref="FeatureStoreCacheConfig.Disabled"/> to this method.
        /// </summary>
        /// <param name="caching">a <see cref="FeatureStoreCacheConfig"/> object specifying caching parameters</param>
        /// <returns>the builder</returns>
        public DynamoDBFeatureStoreBuilder WithCaching(FeatureStoreCacheConfig caching)
        {
            _caching = caching;
            return this;
        }

        /// <summary>
        /// Sets an optional namespace prefix for all keys stored in DynamoDB. Use this if you are sharing
        /// the same database table between multiple clients that are for different LaunchDarkly
        /// environments, to avoid key collisions.
        /// </summary>
        /// <param name="prefix">the namespace prefix</param>
        /// <returns>the builder</returns>
        public DynamoDBFeatureStoreBuilder WithPrefix(string prefix)
        {
            _prefix = prefix;
            return this;
        }
    }
}
