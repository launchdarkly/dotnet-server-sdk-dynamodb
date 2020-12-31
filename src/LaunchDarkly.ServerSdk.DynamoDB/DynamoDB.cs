
namespace LaunchDarkly.Sdk.Server.Integrations
{
    /// <summary>
    /// Integration between the LaunchDarkly SDK and DynamoDB.
    /// </summary>
    public static class DynamoDB
    {
        /// <summary>
        /// Name of the partition key that the data store's table must have. You must specify
        /// this when you create the table. The key type must be String.
        /// </summary>
        public const string DataStorePartitionKey = "namespace";

        /// <summary>
        /// Name of the sort key that the data store's table must have. You must specify this
        /// when you create the table. The key type must be String.
        /// </summary>
        public const string DataStoreSortKey = "key";

        /// <summary>
        /// Returns a builder object for creating a Redis-backed data store.
        /// </summary>
        /// <remarks>
        /// This object can be modified with <see cref="DynamoDBDataStoreBuilder"/> methods for any desired
        /// custom Redis options. Then, pass it to <see cref="Components.PersistentDataStore(Interfaces.IPersistentDataStoreAsyncFactory)"/>
        /// and set any desired caching options. Finally, pass the result to <see cref="ConfigurationBuilder.DataStore(Interfaces.IDataStoreFactory)"/>.
        /// </remarks>
        /// <example>
        /// <code>
        ///     var config = Configuration.Builder("sdk-key")
        ///         .DataStore(
        ///             Components.PersistentDataStore(
        ///                 DynamoDB.DataStore("table-name")
        ///             ).CacheSeconds(15)
        ///         )
        ///         .Build();
        /// </code>
        /// </example>
        /// <param name="tableName">the DynamoDB table name; this table must already exist</param>
        /// <returns>a data store configuration object</returns>
        public static DynamoDBDataStoreBuilder DataStore(string tableName) =>
            new DynamoDBDataStoreBuilder(tableName);
    }
}
