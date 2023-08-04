using Common.Entities;

namespace Common.Events
{
    public record DeliveryNotification
    (
        int customerId,
        int orderId,
        int packageId,
        int sellerId,
        int productId,
        string productName,
        PackageStatus status,
        DateTime deliveryDate,
        int instanceId
    );

}