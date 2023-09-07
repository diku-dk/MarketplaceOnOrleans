namespace Common.Integration;

public class OrderSellerView
{

    public int seller_id { get; set; }

    public int count_orders { get; set; } = 0;
    public int count_items { get; set; } = 0;

    public float total_amount { get; set; } = 0;
    public float total_freight { get; set; } = 0;

    public float total_incentive { get; set; } = 0;

    public float total_invoice { get; set; } = 0;
    public float total_items { get; set; } = 0;

	public OrderSellerView()
	{
	}
}

