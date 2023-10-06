namespace Common.Entities;

public class Shipment
{
    public int order_id { get; set; }
    public int customer_id { get; set; }

	// materialized values from packages
	public int package_count { get; set; }
	public float total_freight_value { get; set; }

	// date all deliveries were requested
    public DateTime request_date { get; set; }

    // shipment status
    public ShipmentStatus status { get; set; }

    // customer delivery address. the same for all packages/sellers
    public string first_name { get; set; }

    public string last_name { get; set; }

    public string street { get; set; }

    public string complement { get; set; }

    public string zip_code { get; set; }

    public string city { get; set; }

    public string state { get; set; }

    public Shipment(){ }
}

