using Common;

namespace Test.Infra;

public class ConfigHelper
{
    public static AppConfig DefaultAppConfig = new()
    {
        StreamReplication = true,
        OrleansTransactions = true,
        OrleansStorage = false,
        AdoNetGrainStorage = false,
        LogRecords = false,
        ConnectionString = PostgresConnectionString,
        NumShipmentActors = 1,
        UseDashboard = false,
        UseSwagger = false,
        UseRedis = true,
        PrimaryConStr = RedisPrimaryConnectionString,
        BackupConStr = RedisBackupConnectionString
    };

    public const string PostgresConnectionString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=password;Pooling=true;Minimum Pool Size=0;Maximum Pool Size=10000";

    public const string RedisPrimaryConnectionString = "localhost:6379";

    public const string RedisBackupConnectionString = "localhost:6380";

}


