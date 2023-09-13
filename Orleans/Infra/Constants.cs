using RocksDbSharp;

namespace Orleans.Infra
{
    public sealed class Constants
    {

        public const string OrleansStorage = "OrleansStorage";
        public const string postgresConnectionString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=password";

        public const string DefaultStreamStorage = "PubSubStore";
        public const string DefaultStreamProvider = "StreamProvider";

        public const string ProductNameSpace = "ProductNs";
        public const string OrderNameSpace = "OrderNs";

        public static readonly Guid ProductStreamId = new("AD713788-B5AE-49FF-8B2C-F311B9CB0CC1");


        public const int NumShipmentActors = 10;

        public static readonly string MarkNamespace = "MarkNs";
        public static readonly Guid CheckoutMarkStreamId = new("AD713788-B5AE-49FF-8B2C-F311B9CB0CC2");
        public static readonly Guid ProductUpdateMarkStreamId = new("AD713788-B5AE-49FF-8B2C-F311B9CB0CC3");

        public static readonly DbOptions rocksDBOptions = new DbOptions()
            .SetCreateIfMissing(true)
            .SetWalDir("WAL") // using WAL
            .SetWalRecoveryMode(Recovery.TolerateCorruptedTailRecords) // setting recovery mode to Absolute Consistency
            .SetAllowConcurrentMemtableWrite(true) // concurrent writers
            .SetEnableWriteThreadAdaptiveYield(true) // required for concurrent writers, see http://smalldatum.blogspot.com/2016/02/concurrent-inserts-and-rocksdb-memtable.html
            .IncreaseParallelism(Environment.ProcessorCount / 2) // only half of all available threads
            .OptimizeLevelStyleCompaction(0);
    }
}
