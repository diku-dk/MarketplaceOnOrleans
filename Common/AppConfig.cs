namespace Common;

public class AppConfig
{
    public bool StreamReplication { get; set; }

    public bool OrleansTransactions { get; set; }

    public bool OrleansStorage { get; set; }

	public bool AdoNetGrainStorage { get; set; }

	public string ConnectionString { get; set; }

    public bool LogRecords { get; set; }

    public int NumShipmentActors { get; set; }

	public bool UseDashboard { get; set; }

    public bool UseSwagger { get; set; }

    public bool UseRedis { get; set; }

    public string PrimaryConStr { get; set; }

    public string BackupConStr { get; set; }        

    public AppConfig(){ }

    public override string ToString()
    {
        return "OrleansTransactions" + OrleansTransactions +
            " \nOrleansStorage" + OrleansStorage +
            " \nAdoNetGrainStorage: "+AdoNetGrainStorage+
            " \nConnectionString: "+ConnectionString+
            " \nNumShipmentActors: "+NumShipmentActors+
            " \nLogRecords: "+LogRecords+
            " \nUseDashboard: "+UseDashboard+
            " \nUseSwagger: "+UseSwagger+
            " \nUseRedis: "+UseRedis+
            " \nPrimaryConStr: "+PrimaryConStr+
            " \nBackupConStr: "+BackupConStr;
    }
}
