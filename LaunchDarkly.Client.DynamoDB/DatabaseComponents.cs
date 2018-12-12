namespace LaunchDarkly.Client.DynamoDB
{
    /// <summary>
    /// Entry point for using the DynamoDB feature store with the LaunchDarkly SDK.
    /// 
    /// For more details about how and why you can use a persistent feature store, see:
    /// https://docs.launchdarkly.com/v2.0/docs/using-a-persistent-feature-store
    /// 
    /// To use the DynamoDB feature store with the LaunchDarkly client, you will first obtain a
    /// builder by calling <see cref="DatabaseComponents.DynamoDBFeatureStore(string)"/>,
    /// then optionally  modify its properties, and then include it in your client configuration.
    /// For example:
    /// 
    /// <code>
    /// using LaunchDarkly.Client;
    /// using LaunchDarkly.Client.DynamoDB;
    /// 
    /// var store = DatabaseComponents.DynamoDBFeatureStore("my-table-name")
    ///     .WithCaching(FeatureStoreCaching.Enabled.WithTtlSeconds(30));
    /// var config = Configuration.Default("my-sdk-key")
    ///     .WithFeatureStoreFactory(store);
    /// </code>
    /// 
    /// Note that the specified table must already exist in DynamoDB. It must have a partition key
    /// of "namespace", and a sort key of "key".
    /// 
    /// By default, the feature store uses a basic DynamoDB client configuration that takes its
    /// AWS credentials and region from AWS environment variables and/or local configuration files.
    /// There are options in the builder for changing some configuration options, or you can
    /// configure the DynamoDB client yourself and pass it to the builder with
    /// <see cref="DynamoDBFeatureStoreBuilder.WithExistingClient(Amazon.DynamoDBv2.AmazonDynamoDBClient)"/>.
    /// 
    /// If you are using the same DynamoDB table as a feature store for multiple LaunchDarkly
    /// environments, use the <see cref="DynamoDBFeatureStoreBuilder.WithPrefix(string)"/>
    /// option and choose a different prefix string for each, so they will not interfere with each
    /// other's data. 
    /// </summary>
    public abstract class DatabaseComponents
    {
        /// <summary>
        /// Creates a builder for a DynamoDB feature store. You can modify any of the store's properties with
        /// <see cref="DynamoDBFeatureStoreBuilder"/> methods before adding it to your client configuration
        /// with <see cref="ConfigurationExtensions.WithFeatureStoreFactory(Configuration, IFeatureStoreFactory)"/>.
        /// </summary>
        /// <param name="tableName">the DynamoDB table name; this table must already exist (see class summary)</param>
        /// <returns>a builder</returns>
        public static DynamoDBFeatureStoreBuilder DynamoDBFeatureStore(string tableName)
        {
            return new DynamoDBFeatureStoreBuilder(tableName);
        }
    }
}
