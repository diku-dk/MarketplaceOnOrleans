using Common.Config;

namespace Test.Infra;

public class ConfigHelper
{
    public static AppConfig TransactionalDefaultAppConfig = new()
    {
        SellerViewPostgres = true,
        StreamReplication = true,
        OrleansTransactions = true,
        OrleansStorage = true,
        AdoNetGrainStorage = false,
        LogRecords = false,
        AdoNetConnectionString = PostgresConnectionString,
        NumShipmentActors = 1,
        UseDashboard = false,
        UseSwagger = false,
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
    };

    public const string PostgresConnectionString = "Host=ep-ancient-wildflower-518871-pooler.eu-central-1.aws.neon.tech;Port=5432;Database=neondb;Username=rodrigolaigner;Password=QtoLT2sbOwP7";

}


