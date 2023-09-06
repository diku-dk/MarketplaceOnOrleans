namespace Common.Events;

public record ProductUpdated(int sellerId, int productId, int instanceId);