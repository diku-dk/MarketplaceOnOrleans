using Common.Entities;
using Common.Requests;

namespace Common.Events
{
    public record ReserveStock
    (
        DateTime timestamp,
        CustomerCheckout customerCheckout,
        IList<CartItem> items,
        int instanceId
    );
}