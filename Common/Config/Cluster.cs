namespace Common.Config;

public class Cluster
{
	public string ClusterId { get; set; }
	public string ServiceId { get; set; }
	public bool Primary { get; set; }
	public string PrimarySiloIpAddress { get; set; }

	public Cluster(){}

}


