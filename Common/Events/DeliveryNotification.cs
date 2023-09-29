using Common.Entities;

namespace Common.Events;

public class DeliveryNotification
{
    public int customerId { get; set; }
    public int orderId { get; set; }
    public int packageId { get; set; }
    public int sellerId { get; set; }
    public int productId { get; set; }
    public string productName { get; set; }
    public PackageStatus status { get; set; }
    public DateTime deliveryDate { get; set; }
    public string instanceId { get; set; }
    
    public DeliveryNotification(){ }

    public DeliveryNotification(int customerId, int orderId, int packageId, int sellerId, int productId, string productName, PackageStatus status, DateTime deliveryDate, string instanceId)
    {
        this.customerId = customerId;
        this.orderId = orderId;
        this.packageId = packageId;
        this.sellerId = sellerId;
        this.productId = productId;
        this.productName = productName;
        this.status = status;
        this.deliveryDate = deliveryDate;
        this.instanceId = instanceId;
    }
}