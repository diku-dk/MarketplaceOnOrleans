using Common.Entities;
using Common.Events;
using Common.Requests;
using Microsoft.Extensions.Logging;
using Orleans.Interfaces;
using Orleans.Runtime;
using Orleans.Streams;

namespace Orleans.Grains
{
    public class ProductActor : Grain, IProductActor
    {

        private IStreamProvider streamProvider;
        private IAsyncStream<ProductUpdate> stream;

        private readonly IPersistentState<Product> product;
        private readonly ILogger<CartActor> _logger;

        public ProductActor([PersistentState(
            stateName: "product",
            storageName: Infra.Constants.OrleansStorage)] IPersistentState<Product> state,
            ILogger<CartActor> _logger)
        {
            this.product = state;
            this._logger = _logger;
        }

        public override async Task OnActivateAsync(CancellationToken token)
        {
            int primaryKey = (int) this.GetPrimaryKeyLong(out string keyExtension);
            var id = string.Format("{0}|{1}", primaryKey, keyExtension);
            _logger.LogInformation("Activating Product actor {0}", id);
            this.streamProvider = this.GetStreamProvider(Infra.Constants.DefaultStreamProvider);
            this.stream = streamProvider.GetStream<ProductUpdate>(Infra.Constants.ProductNameSpace, id);
            await base.OnActivateAsync(token);
        }

        public async Task SetProduct(Product product)
        {
            this.product.State = product;
            ISellerActor sellerActor = this.GrainFactory.GetGrain<ISellerActor>(product.seller_id);
            // notify seller
            await Task.WhenAll( sellerActor.IndexProduct(product.product_id),
                                this.product.WriteStateAsync() );
        }

        public async Task DeleteProduct(DeleteProduct productToDelete)
        {
            // delete from stock 
            if(this.product.State != null)
            {
                this.product.State.active = false;
                await this.product.WriteStateAsync();
                // no need to send delete product to cart? sure we should send it too
                // but must send to stock...
                ProductUpdate productUpdate = new ProductUpdate(
                     productToDelete.sellerId,
                     productToDelete.productId,
                     this.product.State.price,
                     false,
                     productToDelete.instanceId);

                var stockGrain = this.GrainFactory.GetGrain<IStockActor>(productToDelete.sellerId, productToDelete.productId.ToString());
                await stockGrain.DeleteItem();
                return;
            }
            _logger.LogError("State not set in seller {0} product {1}", productToDelete.sellerId, productToDelete.productId);
        }

        public Task<Product> GetProduct()
        {
            return Task.FromResult(this.product.State);
        }

        public async Task UpdatePrice(UpdatePrice updatePrice)
        {
            if (this.product.State is null)
            {
                _logger.LogError("State not set in seller {0} product {1}", updatePrice.sellerId, updatePrice.productId);
                return;
            }

            this.product.State.price = updatePrice.price;
            ProductUpdate productUpdate = new ProductUpdate(
                     updatePrice.sellerId,
                     updatePrice.productId,
                     this.product.State.price,
                     true,
                     updatePrice.instanceId);

            // no way to know which carts contain the product, so require streaming it
            await Task.WhenAll(stream.OnNextAsync(productUpdate), this.product.WriteStateAsync() );
        }
    }
}
