using Common.Entities;

namespace Common.Events;


public class ShipmentNotification
{
    public int customerId { get; set; }
    public int orderId { get; set; }
    public DateTime eventDate { get; set; }
    public int instanceId { get; set; }
    public ShipmentStatus status { get; set; }

    public ShipmentNotification(){ }

    public ShipmentNotification(int customerId, int orderId, DateTime eventDate, int instanceId, ShipmentStatus status)
    {
        this.customerId = customerId;
        this.orderId = orderId;
        this.eventDate = eventDate;
        this.instanceId = instanceId;
        this.status = status;
    }
}
