using Common.Config;
using Common.Entities;
using Common.Integration;
using Common.Requests;
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
            // process new prices as discount
            foreach (var item in this.cart.State.items)
            {
                // query Redis for product price
                string key = item.SellerId + "-" + item.ProductId;
                ProductReplica productReplica = await this.redisFactory.GetProductAsync(key);
                if (item.Version == productReplica.Version)
                {
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
            if (this.redisFactory != null)
            {
                return this.redisFactory.GetProductAsync(key);
            }
            return null;
        }

    }
}
