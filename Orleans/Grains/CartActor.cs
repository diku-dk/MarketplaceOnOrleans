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
    private delegate IOrderActor GetOrderActorDelegate(int customerId);
    protected readonly IPersistentState<Cart> cart;
    protected readonly bool orleansStorage;
    private readonly bool trackHistory;
    protected int customerId;
    private readonly GetOrderActorDelegate callback;
    protected readonly ILogger<CartActor> logger;

    private readonly Dictionary<string,List<CartItem>> history;

    public CartActor([PersistentState(
        stateName: "cart",
        storageName: Constants.OrleansStorage)] IPersistentState<Cart> state,
        AppConfig options,
        ILogger<CartActor> _logger)
    {
        this.cart = state;
        this.callback = options.OrleansTransactions ? GetTransactionalOrderActor : GetOrderActor;
        this.orleansStorage = options.OrleansStorage;
        this.trackHistory = options.TrackCartHistory;
        if(this.trackHistory) history = new Dictionary<string, List<CartItem>>();
        this.logger = _logger;
    }

    public override Task OnActivateAsync(CancellationToken token)
    {
        this.customerId = (int) this.GetPrimaryKeyLong();
        if(this.cart.State is null) {
            this.cart.State = new Cart(this.customerId);
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

        if(this.orleansStorage){
            await this.cart.WriteStateAsync();
        }
    }

    // customer decided to checkout
    public virtual async Task NotifyCheckout(CustomerCheckout customerCheckout)
    {
        // access the orderGrain for this specific order
        var orderActor = this.callback(this.customerId);
        var checkout = new ReserveStock(DateTime.UtcNow, customerCheckout, this.cart.State.items, customerCheckout.instanceId);
        this.cart.State.status = CartStatus.CHECKOUT_SENT;
        try {
            if (this.trackHistory)
            {
                // store cart items internally
                this.history.Add(customerCheckout.instanceId, new(this.cart.State.items));
            }
            await orderActor.Checkout(checkout);
            await this.Seal();
        } catch(Exception e) {
            var str = string.Format("Checkout exception caught in cart ID {0}: {1} - {2} - {3} - {4}", this.customerId, e.StackTrace, e.Source, e.InnerException, e.Data);
            this.logger.LogError(str);
            throw new ApplicationException(str);
        }
    }

    public async Task Seal()
    {
        this.cart.State.status = CartStatus.OPEN;
        this.cart.State.items.Clear();
        if(this.orleansStorage)
            await this.cart.WriteStateAsync();
    }

    public Task<List<CartItem>> GetHistory(string tid)
    {
        if(this.history.ContainsKey(tid))
            return Task.FromResult(this.history[tid]);
        return Task.FromResult(new List<CartItem>());
    }

    private IOrderActor GetOrderActor(int customerId)
    {
        return this.GrainFactory.GetGrain<IOrderActor>(customerId);
    }

    private ITransactionalOrderActor GetTransactionalOrderActor(int customerId)
    {
        return this.GrainFactory.GetGrain<ITransactionalOrderActor>(customerId);
    }

}