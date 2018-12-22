using Amazon.DynamoDBv2;
using Amazon.Runtime;
using LaunchDarkly.Client.Utils;

namespace LaunchDarkly.Client.DynamoDB
{
    /// <summary>
    /// Builder for a DynamoDB-based implementation of <see cref="IFeatureStore"/>.
    /// Create an instance of the builder by calling <see cref="DynamoDBComponents.DynamoDBFeatureStore"/>;
    /// configure it using the setter methods; then pass the builder to
    /// <see cref="ConfigurationExtensions.WithFeatureStore(Configuration, IFeatureStore)"/>.
    /// 
    /// The AWS SDK provides many configuration options for a DynamoDB client. This class has
    /// corresponding methods for some of the most commonly used ones, but also allows you to use
    /// AWS SDK classes to access the full range of options.
    /// </summary>
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
        /// <param name="caching">a <see cref="FeatureStoreCaching"/> object specifying caching parameters</param>
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
