namespace Common;

public class AppConfig
{
    public bool OrleansStorage { get; set; }

	public bool AdoNetGrainStorage { get; set; }

	public string ConnectionString { get; set; }

    public bool LogRecords { get; set; }

    public int  NumShipmentActors { get; set; }

	public bool UseDashboard { get; set; }

    public bool UseSwagger { get; set; }

    public AppConfig(){ }

    public override string ToString()
    {
        return "OrleansStorage"+AdoNetGrainStorage+
            " \nAdoNetGrainStorage: "+AdoNetGrainStorage+
            " \nConnectionString: "+ConnectionString+
            " \nNumShipmentActors: "+NumShipmentActors+
            " \nLogRecords: "+LogRecords+
            " \nUseDashboard: "+UseDashboard+
            " \nUseSwagger: "+UseSwagger;
    }
}
