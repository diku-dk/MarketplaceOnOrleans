using Common.Entities;
using Common.Events;
using Common.Requests;
using Microsoft.Extensions.Logging;
using Orleans.Infra;
using Orleans.Interfaces;
using Orleans.Runtime;
using Orleans.Streams;

namespace Orleans.Grains;

public class CartActor : Grain, ICartActor
{
    //private IStreamProvider streamProvider;
    //private IAsyncStream<ReserveStock> stream;
    //private List<StreamSubscriptionHandle<ProductUpdate>> consumerHandles;

    private readonly IPersistentState<Cart> cart;

    private int customerId;
    private readonly ILogger<CartActor> _logger;

    //private readonly Dictionary<(int seller_id, int product_id), ProductUpdate> cachedProducts;

    public CartActor([PersistentState(
        stateName: "cart",
        storageName: Constants.OrleansStorage)] IPersistentState<Cart> state, 
        ILogger<CartActor> _logger)
    {
        this.cart = state;
        this._logger = _logger;
        //this.consumerHandles = new();
        //this.cachedProducts = new();
    }

    public override async Task OnActivateAsync(CancellationToken token)
    {
        this.customerId = (int) this.GetPrimaryKeyLong();
        if(cart.State is null) cart.State = new Cart();
        //this.streamProvider = this.GetStreamProvider(Constants.DefaultStreamProvider);
        //this.stream = streamProvider.GetStream<ReserveStock>(Constants.OrderNameSpace, this.customerId.ToString());
        await base.OnActivateAsync(token);
    }

    //public async Task BecomeConsumer(string id)
    //{ 
    //    IAsyncStream<ProductUpdate> _consumer = streamProvider.GetStream<ProductUpdate>(Constants.ProductNameSpace, id);
    //    this.consumerHandles.Add( await _consumer.SubscribeAsync(UpdateProductAsync) );
    //}

    //public async Task StopConsuming()
    //{
    //    this._logger.LogInformation("StopConsuming");
    //    if (this.consumerHandles.Count() > 0)
    //    {
    //        foreach(var handle in consumerHandles) { await handle.UnsubscribeAsync(); }
    //    }
    //    this.consumerHandles.Clear();
    //}

    //private Task UpdateProductAsync(ProductUpdate product, StreamSequenceToken token)
    //{
    //    this.cachedProducts.Add((product.seller_id, product.product_id), product);
    //    return Task.CompletedTask;
    //}

    public async Task AddItem(CartItem item)
    {
        if (item.Quantity <= 0)
        {
            throw new Exception("Item " + item.ProductId + " shows no positive quantity.");
        }

        if (cart.State.status == CartStatus.CHECKOUT_SENT)
        {
            throw new Exception("Cart for customer " + this.customerId + " already sent for checkout.");
        }

        this.cart.State.items.Add(item);
        await this.cart.WriteStateAsync();
        //BecomeConsumer(string.Format("{0}|{1}", item.SellerId, item.ProductId) ) );
    }

    public Task<Cart> GetCart()
    {
        this._logger.LogWarning("Cart {0} GET cart request.", this.customerId);
        return Task.FromResult(this.cart.State);
    }

    // customer decided to checkout
    public async Task NotifyCheckout(CustomerCheckout customerCheckout)
    {
        this._logger.LogWarning("Cart {0} received checkout request.", this.customerId);

        // access the orderGrain for this specific order
        var orderActor = this.GrainFactory.GetGrain<IOrderActor>(customerId);
        var checkout = new ReserveStock(DateTime.UtcNow, customerCheckout, cart.State.items, customerCheckout.instanceId);
        cart.State.status = CartStatus.CHECKOUT_SENT;
        await orderActor.Checkout(checkout);    // TODO: need to check if the request returned immediately even with 'await'
        cart.State.status = CartStatus.OPEN;
        this.cart.State.items.Clear();
        this._logger.LogWarning($"Send CheckoutOrder request to the order actor. ");
        await this.cart.WriteStateAsync();
    }

    public async Task Seal()
    {
        this.cart.State.items.Clear();
        await Task.WhenAll(
            // this.cart.WriteStateAsync(),
            // this.StopConsuming()
         );
    }
}