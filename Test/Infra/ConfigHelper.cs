using Common.Config;

namespace Test.Infra;

public class ConfigHelper
{
    public static AppConfig TransactionalDefaultAppConfig = new()
    {
        SellerViewPostgres = true,
        StreamReplication = true,
        OrleansTransactions = true,
        OrleansStorage = false,
        AdoNetGrainStorage = false,
        LogRecords = false,
        AdoNetConnectionString = PostgresConnectionString,
        NumShipmentActors = 1,
        UseDashboard = false,
        UseSwagger = false,
        TrackCartHistory = true
    };

    public static AppConfig NonTransactionalDefaultAppConfig = new()
    {
        SellerViewPostgres = false,
        StreamReplication = false,
        OrleansTransactions = false,
        OrleansStorage = true,
        AdoNetGrainStorage = false,
        LogRecords = false,
        AdoNetConnectionString = PostgresConnectionString,
        NumShipmentActors = 1,
        UseDashboard = false,
        UseSwagger = false,
        TrackCartHistory = false
    };

    public const string PostgresConnectionString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=password;Pooling=true;Minimum Pool Size=0;Maximum Pool Size=10000";

}


