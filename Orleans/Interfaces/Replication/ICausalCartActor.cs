using Common.Entities;
using Common.Integration;
using OrleansApp.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orleans.Interfaces.Replication
{
    public interface ICausalCartActor : ICartActor
    {
        public Task<ProductReplica> GetReplicaItem(int sellerId, int productId);
    }
}

