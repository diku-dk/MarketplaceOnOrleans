using Common.Entities;

namespace Common.Events
{
    public record ShipmentNotification
    (
        int customerId,
        int orderId,
        DateTime eventDate,
        int instanceId,
        ShipmentStatus status = ShipmentStatus.approved
    );
}