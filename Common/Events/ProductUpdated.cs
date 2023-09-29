namespace Common.Events;

public class ProductUpdated {

    public int sellerId { get; set; }
    public int productId { get; set; }
    public string instanceId { get; set; }

    public ProductUpdated(){ }

    public ProductUpdated(int sellerId, int productId, string instanceId)
    {
        this.sellerId = sellerId;
        this.productId = productId;
        this.instanceId = instanceId;
    }
}