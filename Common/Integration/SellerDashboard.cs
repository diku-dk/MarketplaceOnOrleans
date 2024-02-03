using Common.Entities;

namespace Common.Integration;

public sealed class SellerDashboard
{
	public OrderSellerView sellerView { get; set; }
	public List<OrderEntry> orderEntries { get; set; }

    public SellerDashboard(){ }

    public SellerDashboard(OrderSellerView sellerView, List<OrderEntry> orderEntries)
    {
        this.sellerView = sellerView;
        this.orderEntries = orderEntries;
    }
}

