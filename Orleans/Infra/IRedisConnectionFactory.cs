using Common.Integration;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Orleans.Infra
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

    public sealed class RedisConnectionFactoryImpl : IRedisConnectionFactory
    {
        private readonly Dictionary<string, IConnectionMultiplexer> _connections = new();

        public RedisConnectionFactoryImpl(string primaryConStr, string backupConStr)
        {
            // stackexchange.github.io/StackExchange.Redis/Configuration.html

            _connections["Primary"] = ConnectionMultiplexer.Connect(primaryConStr);
            _connections["ReadOnlyBackup"] = ConnectionMultiplexer.Connect(backupConStr);
        }

        public IConnectionMultiplexer GetConnection(string name)
        {
            return _connections.TryGetValue(name, out var connection) ? connection : null;
        }

        public async Task<bool> SaveProductAsync(string key, ProductReplica productCaches)
        {
            var db = _connections["Primary"].GetDatabase();
            var value = JsonConvert.SerializeObject(productCaches);
            return await db.StringSetAsync(key, value);
        }

        public async Task<bool> UpdateProductAsync(string key, ProductReplica productCaches)
        {
            var db = _connections["Primary"].GetDatabase();
            var value = JsonConvert.SerializeObject(productCaches);
            return await db.StringSetAsync(key, value);
        }

        public async Task<ProductReplica> GetProductAsync(string key)
        {
            var db = _connections["ReadOnlyBackup"].GetDatabase();
            var value = await db.StringGetAsync(key);
            return value.HasValue ? JsonConvert.DeserializeObject<ProductReplica>(value) : null;
        }

    }


}
