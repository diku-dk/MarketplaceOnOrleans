using Common.Entities;

namespace Common.Integration;

public record SellerDashboard
(
	OrderSellerView sellerView,
	IList<OrderEntry> orderEntries
);

