using Common.Entities;
using OrleansApp.Interfaces;

namespace Orleans.Interfaces.Replication;

public interface IEventualCartActor : ICartActor
{
    public Task<Product> GetReplicaItem(int sellerId, int productId);
}