using Common.Entities;

namespace Common.Integration;

public record SellerDashboard
(
	OrderSellerView sellerView,
	List<OrderEntry> orderEntries
);

