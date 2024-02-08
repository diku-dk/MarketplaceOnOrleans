using Common.Integration;
using OrleansApp.Interfaces;

namespace Orleans.Interfaces.Replication
{
    public interface ICausalCartActor : ICartActor
    {
        public Task<ProductReplica> GetReplicaItem(int sellerId, int productId);
    }
}

