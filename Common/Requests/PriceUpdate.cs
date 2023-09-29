namespace Common.Requests;

public class PriceUpdate
{

    public int sellerId { get; set; }
    public int productId { get; set; }
    public float price { get; set; }
    public string instanceId { get; set; }

    public PriceUpdate(){ }

    public PriceUpdate(int sellerId, int productId, float price, string instanceId)
    {
        this.sellerId = sellerId;
        this.productId = productId;
        this.price = price;
        this.instanceId = instanceId;
    }
}