using System;

namespace LaunchDarkly.Client.DynamoDB
{
    /// <summary>
    /// Obsolete entry point for the DynamoDB integration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is retained in version 1.2 of the library for backward compatibility. For the new
    /// preferred way to configure the DynamoDB integration, see <see cref="LaunchDarkly.Client.Integrations.DynamoDB"/>.
    /// Updating to the latter now will make it easier to adopt version 6.0 of the LaunchDarkly .NET SDK, since
    /// an identical API is used there (except for the base namespace).
    /// </para>
    /// </remarks>
    [Obsolete("Use LaunchDarkly.Client.Integrations.DynamoDB")]
    public abstract class DynamoDBComponents
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
