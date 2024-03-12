using Common.Entities;
using Microsoft.Extensions.Logging;
using Orleans.Interfaces.Replication;
using Orleans.Runtime;
using OrleansApp.Grains;
using Orleans.Streams;
using Common.Requests;
using OrleansApp.Infra;
using Common.Config;

namespace Orleans.Grains.Replication;

/**
 * This actor receives product updates through Orleans Streams
 */
public sealed class EventualCartActor : CartActor, IEventualCartActor
{
    private IStreamProvider streamProvider;
    // private IAsyncStream<Product> stream;
    private List<StreamSubscriptionHandle<Product>> consumerHandles;

    private readonly Dictionary<(int SellerId, int ProductId), Product> cachedProducts;

    public EventualCartActor(
        [PersistentState("cart", Constants.OrleansStorage)] IPersistentState<Cart> state, 
        AppConfig options, 
        ILogger<CartActor> _logger) : base(state, options, _logger)
    {
        this.consumerHandles = new List<StreamSubscriptionHandle<Product>>();
        this.cachedProducts = new();
    }

    public override Task OnActivateAsync(CancellationToken token)
    {
        base.OnActivateAsync(token);
        this.streamProvider = this.GetStreamProvider(Constants.DefaultStreamProvider);
        return Task.CompletedTask;
    }

    public async Task BecomeConsumer(string id)
    { 
        IAsyncStream<Product> _consumer = this.streamProvider.GetStream<Product>(Constants.ProductNameSpace, id);
        this.consumerHandles.Add(await _consumer.SubscribeAsync(UpdateProductAsync) );
    }

    public async Task StopConsuming()
    {
        List<Task> toWait = new List<Task>(this.consumerHandles.Count);
        foreach (var handle in this.consumerHandles) { 
            toWait.Add( handle.UnsubscribeAsync() );
        }
        await Task.WhenAll( toWait );
        this.consumerHandles.Clear();
    }

    private Task UpdateProductAsync(Product product, StreamSequenceToken token)
    {
        if(this.cachedProducts.ContainsKey((product.seller_id, product.product_id))){
            this.cachedProducts[(product.seller_id, product.product_id)] = product;
        } else
        {
            this.cachedProducts.Add((product.seller_id, product.product_id), product);
        }

        return Task.CompletedTask;
    }

    public override async Task AddItem(CartItem item)
    {
        await this.BecomeConsumer( string.Format("{0}|{1}", item.SellerId, item.ProductId) );
        await base.AddItem(item);
    }

    public override async Task NotifyCheckout(CustomerCheckout customerCheckout)
    {
        // process new prices as discount
        foreach(var item in this.cart.State.items)
        {
            var ID = (item.SellerId, item.ProductId);
            if (this.cachedProducts.ContainsKey(ID))
            {
                Product product = this.cachedProducts[ID];
                if( item.Version.SequenceEqual(product.version) && item.UnitPrice < product.price ){
                    item.UnitPrice = product.price;
                    item.Voucher += product.price - item.UnitPrice;
                }
            }
        }
        await base.NotifyCheckout(customerCheckout);
        // stop consuming
        await this.StopConsuming();
    }

    public Task<Product> GetReplicaItem(int sellerId, int productId)
    {
        return Task.FromResult(this.cachedProducts[(sellerId, productId)]);
    }

}

