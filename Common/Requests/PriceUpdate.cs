namespace Common.Requests;

public record PriceUpdate(int sellerId, int productId, float price, int instanceId);