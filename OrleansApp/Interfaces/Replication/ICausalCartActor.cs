using Common.Integration;

namespace OrleansApp.Interfaces.Replication;

public interface ICausalCartActor : ICartActor
{
    public Task<ProductReplica> GetReplicaItem(int sellerId, int productId);
}
