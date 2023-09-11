using Common.Entities;
using Common.Events;
using Common.Requests;
using Microsoft.Extensions.Logging;
using Orleans.Infra;
using Orleans.Interfaces;
using Orleans.Runtime;

namespace Orleans.Grains;

public class CartActor : Grain, ICartActor
{

    private readonly IPersistentState<Cart> cart;

    private int customerId;
    private readonly ILogger<CartActor> _logger;

    public CartActor([PersistentState(
        stateName: "cart",
        storageName: Constants.OrleansStorage)] IPersistentState<Cart> state, 
        ILogger<CartActor> _logger)
    {
        this.cart = state;
        this._logger = _logger;
    }

    public override async Task OnActivateAsync(CancellationToken token)
    {
        this.customerId = (int) this.GetPrimaryKeyLong();
        if(cart.State is null) {
            cart.State = new Cart();
            cart.State.customerId = this.customerId;
        }
        await base.OnActivateAsync(token);
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
        await this.cart.WriteStateAsync();
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
        this._logger.LogWarning($"Send CheckoutOrder request to the order actor. ");
        await orderActor.Checkout(checkout);
        cart.State.status = CartStatus.OPEN;
        this.cart.State.items.Clear();
        await this.cart.WriteStateAsync();
    }

    public async Task Seal()
    {
        this.cart.State.items.Clear();
        await this.cart.WriteStateAsync();
    }
}