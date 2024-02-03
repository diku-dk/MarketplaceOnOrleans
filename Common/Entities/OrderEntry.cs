namespace Common.Entities;

public class OrderEntry
{
    public int customer_id { get; set; }

    public int order_id { get; set; }

    public int product_id { get; set; }

    public int seller_id { get; set; }

    public int? package_id { get; set; }

    public string product_name { get; set; }

    public string product_category { get; set; }

    public float unit_price { get; set; }

    public int quantity { get; set; }

    public float total_items { get; set; }

    public float total_amount { get; set; }

    public float total_incentive { get; set; }

    public float total_invoice { get; set; } = 0;

    public float freight_value { get; set; }

    public DateTime? shipment_date { get; set; }

    public DateTime? delivery_date { get; set; }

    // denormalized, thus redundant. to avoid join on details
    public OrderStatus order_status { get; set; }

    public PackageStatus delivery_status { get; set; }

    public OrderEntry(){ }

}

