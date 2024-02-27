namespace Common.Integration;

public class OrderSellerView
{
    public int seller_id { get; set; }

    public int count_orders { get; set; }

    public int count_items { get; set; }

    public float total_amount { get; set; }

    public float total_freight { get; set; }

    public float total_incentive { get; set; }

    public float total_invoice { get; set; }

    public float total_items { get; set; }

	public OrderSellerView()
	{
	}

    public OrderSellerView(int sellerId)
    {
        this.seller_id = sellerId;
    }

}

