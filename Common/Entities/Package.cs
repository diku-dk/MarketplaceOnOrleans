namespace Common.Entities;

public class Package
{
    public int shipment_id { get; set; }

	// PK
	public int order_id { get; set; }
    public int customer_id { get; set; }
	public int package_id { get; set; }

    // FK
    // product identification
    public int seller_id { get; set; }
    public int product_id { get; set; }

    public string product_name { get; set; }

    public float freight_value { get; set; }

	// date the shipment has actually been performed
	public DateTime shipping_date { get; set; }

    // delivery date
    public DateTime delivery_date { get; set; }

	public int quantity { get; set; }
        
    public PackageStatus status { get; set; }

    public Package() { }
}

