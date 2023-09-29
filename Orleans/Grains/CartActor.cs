using Common;
using Common.Entities;
using Common.Events;
using Common.Requests;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Infra;
using Orleans.Interfaces;
using Orleans.Runtime;

namespace Orleans.Grains;

public class CartActor : Grain, ICartActor
{
    private readonly IPersistentState<Cart> cart;
    private readonly AppConfig config;
    private int customerId;
    private readonly ILogger<CartActor> _logger;

    public CartActor([PersistentState(
        stateName: "cart",
        storageName: Constants.OrleansStorage)] IPersistentState<Cart> state,
        IOptions<AppConfig> options,
        ILogger<CartActor> _logger)
    {
        this.cart = state;
        this.config = options.Value;
        this._logger = _logger;
    }

    public override Task OnActivateAsync(CancellationToken token)
    {
        this.customerId = (int) this.GetPrimaryKeyLong();
        if(cart.State is null) {
            cart.State = new Cart();
            cart.State.customerId = this.customerId;
        }
        return Task.CompletedTask;
    }

    public Task<Cart> GetCart()
    {
        return Task.FromResult(this.cart.State);
    }

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

        if(config.OrleansStorage)
            await this.cart.WriteStateAsync();
    }

    // customer decided to checkout
    public async Task NotifyCheckout(CustomerCheckout customerCheckout)
    {
        // access the orderGrain for this specific order
        var orderActor = this.GrainFactory.GetGrain<IOrderActor>(this.customerId);
        var checkout = new ReserveStock(DateTime.UtcNow, customerCheckout, cart.State.items, customerCheckout.instanceId);
        cart.State.status = CartStatus.CHECKOUT_SENT;
        try{
            await orderActor.Checkout(checkout);
        } catch(Exception e){
            _logger.LogError("Exception captured in actor {0}. Source: {1} Message: {2}", customerId, e.Source, e.StackTrace);
        } finally{
            await Seal();
        }
    }

    public async Task Seal()
    {
        cart.State.status = CartStatus.OPEN;
        this.cart.State.items.Clear();
        if(config.OrleansStorage)
            await this.cart.WriteStateAsync();
    }
}