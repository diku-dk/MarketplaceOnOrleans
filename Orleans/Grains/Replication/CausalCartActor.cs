using Common.Config;
using Common.Entities;
using Common.Integration;
using Common.Requests;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Logging;
using Orleans.Infra.Redis;
using Orleans.Interfaces.Replication;
using Orleans.Runtime;
using OrleansApp.Grains;
using OrleansApp.Infra;

namespace Orleans.Grains.Replication
{

    /**
     * This actor update prices (if inconsistency) by querying Redis before checkout
     */
    public sealed class CausalCartActor : CartActor, ICausalCartActor
    {
        private readonly IRedisConnectionFactory redisFactory;

        public CausalCartActor(
            [PersistentState("cart", Constants.OrleansStorage)] IPersistentState<Cart> state,
            AppConfig options,
            ILogger<CartActor> _logger,
            IRedisConnectionFactory factory) : base(state, options, _logger)
        {
            this.redisFactory = factory;
        }

        public override async Task NotifyCheckout(CustomerCheckout customerCheckout)
        {
            foreach (var item in this.cart.State.items)
            {
                // query Redis for product price
                ProductReplica productReplica = await this.GetReplicaItem(item.SellerId, item.ProductId);

                if(productReplica is null)
                {
                    this.logger.LogWarning($"Item {item.SellerId} - {item.ProductId} cannot be found in Redis replica");
                    continue;
                }

                // same version?
                if (item.Version.SequenceEqual( productReplica.Version ))
                {
                    // process new prices as discount
                    if (item.UnitPrice < productReplica.Price)
                    {
                        item.Voucher += productReplica.Price - item.UnitPrice;
                    }
                }
                // if product version is different, old price can be used as the product has been deleted                                   
            }
            await base.NotifyCheckout(customerCheckout);
        }

        public Task<ProductReplica> GetReplicaItem(int sellerId, int productId)
        {
            string key = sellerId + "-" + productId;
            return this.redisFactory.GetProductAsync(key);
        }

    }
}
