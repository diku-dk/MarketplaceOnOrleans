using Common.Entities;
using Common.Events;
using Common.Requests;
using Microsoft.Extensions.Logging;
using OrleansApp.Infra;
using OrleansApp.Interfaces;
using Orleans.Runtime;
using OrleansApp.Transactional;
using Common.Config;

namespace OrleansApp.Grains;

public class CartActor : Grain, ICartActor
{
    protected readonly IPersistentState<Cart> cart;
    protected readonly AppConfig config;
    protected int customerId;
    protected readonly ILogger<CartActor> logger;
    private readonly GetOrderActorDelegate callback;

    public CartActor([PersistentState(
        stateName: "cart",
        storageName: Constants.OrleansStorage)] IPersistentState<Cart> state,
        AppConfig options,
        ILogger<CartActor> _logger)
    {
        this.cart = state;
        this.config = options;
        this.callback = config.OrleansTransactions ? GetTransactionalOrderActor : GetOrderActor;
        this.logger = _logger;
    }

    public override Task OnActivateAsync(CancellationToken token)
    {
        this.customerId = (int) this.GetPrimaryKeyLong();
        if(this.cart.State is null) {
            this.cart.State = new Cart();
            this.cart.State.customerId = this.customerId;
        }
        return Task.CompletedTask;
    }

    public Task<Cart> GetCart()
    {
        return Task.FromResult(this.cart.State);
    }

    public virtual async Task AddItem(CartItem item)
    {
        if (item.Quantity <= 0)
        {
            throw new Exception("Item " + item.ProductId + " shows no positive quantity.");
        }

        if (this.cart.State.status == CartStatus.CHECKOUT_SENT)
        {
            throw new Exception("Cart for customer " + this.customerId + " already sent for checkout.");
        }

        this.cart.State.items.Add(item);

        if(config.OrleansStorage)
            await this.cart.WriteStateAsync();
    }

    // customer decided to checkout
    public virtual async Task NotifyCheckout(CustomerCheckout customerCheckout)
    {
        // access the orderGrain for this specific order
        var orderActor = this.callback(this.customerId);
        var checkout = new ReserveStock(DateTime.UtcNow, customerCheckout, this.cart.State.items, customerCheckout.instanceId);
        this.cart.State.status = CartStatus.CHECKOUT_SENT;
        try {
            await orderActor.Checkout(checkout);
            await this.Seal();
        } catch(Exception e) {
            this.logger.LogError("Checkout exception caught in cart ID {0}: {1} - {2} - {3} - {4}", this.customerId, e.StackTrace, e.Source, e.InnerException, e.Data);
            throw;
        }
    }

    private delegate IOrderActor GetOrderActorDelegate(int customerId);

    private IOrderActor GetOrderActor(int customerId)
    {
        return this.GrainFactory.GetGrain<IOrderActor>(customerId);
    }

    private ITransactionalOrderActor GetTransactionalOrderActor(int customerId)
    {
        return this.GrainFactory.GetGrain<ITransactionalOrderActor>(customerId);
    }

    public async Task Seal()
    {
        this.cart.State.status = CartStatus.OPEN;
        this.cart.State.items.Clear();
        if(this.config.OrleansStorage)
            await this.cart.WriteStateAsync();
    }

}