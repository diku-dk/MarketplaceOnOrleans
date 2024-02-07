namespace Common.Config;

public sealed class AppConfig
{
    public bool SellerViewPostgres { get; set; }

    public bool StreamReplication { get; set; }

    public bool RedisReplication { get; set; }

    public string RedisPrimaryConnectionString { get; set; }

    public string RedisSecondaryConnectionString { get; set; }

    public bool OrleansTransactions { get; set; }

    public bool OrleansStorage { get; set; }

    public bool AdoNetGrainStorage { get; set; }

    public string AdoNetConnectionString { get; set; }

    public bool LogRecords { get; set; }

    public int NumShipmentActors { get; set; }

    public bool UseDashboard { get; set; }

    public bool UseSwagger { get; set; }

    public AppConfig() { }

    public override string ToString()
    {
        return "OrleansTransactions" + OrleansTransactions +
            " \nOrleansStorage" + OrleansStorage +
            " \nStreamReplication" + StreamReplication +
            " \nSellerViewPostgres" + SellerViewPostgres +
            " \nAdoNetGrainStorage: " + AdoNetGrainStorage +
            " \nAdoNetConnectionString: " + AdoNetConnectionString +
            " \nNumShipmentActors: " + NumShipmentActors +
            " \nLogRecords: " + LogRecords +
            " \nUseDashboard: " + UseDashboard +
            " \nUseSwagger: " + UseSwagger +
            " \nRedisReplication: " + RedisReplication +
            " \nRedisPrimaryConnectionString: " + RedisPrimaryConnectionString +
            " \nRedisSecondaryConnectionString: " + RedisSecondaryConnectionString;
    }
}
