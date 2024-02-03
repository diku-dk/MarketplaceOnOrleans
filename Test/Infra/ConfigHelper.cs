using Common.Config;

namespace Test.Infra;

public class ConfigHelper
{
    public static AppConfig TransactionalDefaultAppConfig = new()
    {
        StreamReplication = true,
        OrleansTransactions = true,
        OrleansStorage = true,
        AdoNetGrainStorage = false,
        LogRecords = false,
        ConnectionString = PostgresConnectionString,
        NumShipmentActors = 1,
        UseDashboard = false,
        UseSwagger = false,
    };

    public static AppConfig NonTransactionalDefaultAppConfig = new()
    {
        StreamReplication = true,
        OrleansTransactions = false,
        OrleansStorage = true,
        AdoNetGrainStorage = false,
        LogRecords = false,
        ConnectionString = PostgresConnectionString,
        NumShipmentActors = 1,
        UseDashboard = false,
        UseSwagger = false,
    };

    public const string PostgresConnectionString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=password;Pooling=true;Minimum Pool Size=0;Maximum Pool Size=10000"; 
}


