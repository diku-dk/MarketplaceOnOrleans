using Common.Entities;

namespace OrleansApp.Interfaces.Replication;

public interface IEventualCartActor : ICartActor
{
    public Task<Product> GetReplicaItem(int sellerId, int productId);
}

