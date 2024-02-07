using Common.Entities;
using Common.Integration;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orleans.Infra
{

    public interface IRedisConnectionFactory
    {
        IConnectionMultiplexer GetConnection(string name);

        Task<bool> SaveProductAsync(string key, ProductReplica productCache);

        Task<bool> UpdateProductAsync(string key, ProductReplica productCache);

        Task<ProductReplica> GetProductAsync(string key);
    }

    public class RedisConnectionFactory : IRedisConnectionFactory
    {
        private readonly Dictionary<string, IConnectionMultiplexer> _connections = new();

        public RedisConnectionFactory(string primaryConStr, string backupConStr)
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
