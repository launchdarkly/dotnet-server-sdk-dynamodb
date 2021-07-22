
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
        /// Returns a builder object for creating a DynamoDB-backed data store.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This can be used either for the main data store that holds feature flag data, or for the big
        /// segment store, or both. If you are using both, they do not have to have the same parameters. For
        /// instance, in this example the main data store uses a table called "table1" and the big segment
        /// store uses a table called "table2":
        /// </para>
        /// <code>
        ///     var config = Configuration.Builder("sdk-key")
        ///         .DataStore(
        ///             Components.PersistentDataStore(
        ///                 DynamoDB.DataStore("table1")
        ///             )
        ///         )
        ///         .BigSegments(
        ///             Components.BigSegments(
        ///                 DynamoDB.DataStore("table2")
        ///             )
        ///         )
        ///         .Build();
        /// </code>
        /// <para>
        /// Note that the builder is passed to one of two methods,
        /// <see cref="Components.PersistentDataStore(LaunchDarkly.Sdk.Server.Interfaces.IPersistentDataStoreAsyncFactory)"/> or
        /// <see cref="Components.BigSegments(LaunchDarkly.Sdk.Server.Interfaces.IBigSegmentStoreFactory)"/>, depending on the context in
        /// which it is being used. This is because each of those contexts has its own additional
        /// configuration options that are unrelated to the DynamoDB options. For instance, the
        /// <see cref="Components.PersistentDataStore(LaunchDarkly.Sdk.Server.Interfaces.IPersistentDataStoreAsyncFactory)"/> builder
        /// has options for caching:
        /// </para>
        /// <code>
        ///     var config = Configuration.Builder("sdk-key")
        ///         .DataStore(
        ///             Components.PersistentDataStore(
        ///                 DynamoDB.DataStore("table1")
        ///             ).CacheSeconds(15)
        ///         )
        ///         .Build();
        /// </code>
        /// </remarks>
        /// <param name="tableName">the DynamoDB table name; this table must already exist</param>
        /// <returns>a data store configuration object</returns>
        public static DynamoDBDataStoreBuilder DataStore(string tableName) =>
            new DynamoDBDataStoreBuilder(tableName);
    }
}
