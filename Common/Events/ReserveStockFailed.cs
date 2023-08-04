using Common.Entities;
using Common.Requests;

namespace Common.Events
{
    public record ReserveStockFailed
    (
        DateTime timestamp,
        CustomerCheckout customerCheckout,
        List<ProductStatus> products,
        int instanceId
    );
}

