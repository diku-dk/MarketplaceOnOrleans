using Common.Integration;
using StackExchange.Redis;

namespace Orleans.Infra.Redis
{

    public interface IRedisConnectionFactory
    {
        IConnectionMultiplexer GetConnection(string name);

        Task<bool> SaveProductAsync(string key, ProductReplica productCache);

        Task<bool> UpdateProductAsync(string key, ProductReplica productCache);

        Task<ProductReplica> GetProductAsync(string key);
    }

    public sealed class EtcNullConnectionFactoryImpl : IRedisConnectionFactory
    {
        public IConnectionMultiplexer GetConnection(string name)
        {
            throw new NotImplementedException();
        }

        public Task<ProductReplica> GetProductAsync(string key)
        {
            throw new NotImplementedException();
        }

        public Task<bool> SaveProductAsync(string key, ProductReplica productCache)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateProductAsync(string key, ProductReplica productCache)
        {
            throw new NotImplementedException();
        }
    }

}
