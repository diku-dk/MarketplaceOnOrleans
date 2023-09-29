namespace Common;

public class AppConfig
{
    public bool OrleansStorage { get; set; }

	public bool MemoryGrainStorage { get; set; }

	public string ConnectionString { get; set; }

    public bool LogRecords { get; set; }

    public int  NumShipments { get; set; }

	public bool UseDashboard { get; set; }

    public bool UseSwagger { get; set; }

    public override string ToString()
    {
        return "MemoryGrainStorage: "+MemoryGrainStorage+" \nConnectionString: "+ConnectionString+" \nUseDashboard: "+UseDashboard;
    }
}
