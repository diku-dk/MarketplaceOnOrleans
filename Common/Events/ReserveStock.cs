using Common.Entities;
using Common.Requests;

namespace Common.Events
{
    public record ReserveStock
    (
        DateTime timestamp,
        CustomerCheckout customerCheckout,
        List<CartItem> items,
        int instanceId
    );
}