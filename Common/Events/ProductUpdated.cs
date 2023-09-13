namespace Common.Events;

public class ProductUpdated {

    public int sellerId { get; set; }
    public int productId { get; set; }
    public int instanceId { get; set; }

    public ProductUpdated(){ }

    public ProductUpdated(int sellerId, int productId, int instanceId)
    {
        this.sellerId = sellerId;
        this.productId = productId;
        this.instanceId = instanceId;
    }
}