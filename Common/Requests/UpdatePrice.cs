namespace Common.Requests
{
    public record UpdatePrice(int sellerId, int productId, float price, int instanceId);
}