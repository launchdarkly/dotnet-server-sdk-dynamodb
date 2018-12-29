using System;
using System.Collections.Generic;
using System.Text;

namespace LaunchDarkly.Client.DynamoDB
{
    /// <summary>
    /// Constants used by the LaunchDarkly DynamoDB feature store. You may wish to use these if
    /// you create your DynamoDB table programmatically.
    /// </summary>
    public class Constants
    {
        /// <summary>
        /// Name of the partition key that the feature store's table must have. You must specify
        /// this when you create the table. The key type must be String.
        /// </summary>
        public const string PartitionKey = "namespace";

        /// <summary>
        /// Name of the sort key that the feature store's table must have. You must specify this
        /// when you create the table. The key type must be String.
        /// </summary>
        public const string SortKey = "key";
    }
}
