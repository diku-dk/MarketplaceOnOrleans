using Common.Entities;
using Common.Events;
using Common.Requests;
using Microsoft.Extensions.Logging;
using OrleansApp.Infra;
using OrleansApp.Interfaces;
using OrleansApp.Transactional;
using Common.Config;

namespace OrleansApp.Grains;

public class CartActor : Grain, ICartActor
{
    protected readonly IPersistentState<Cart> cart;
    protected readonly bool orleansStorage;
    private readonly bool trackHistory;
    protected int customerId;
    private readonly bool OrleansTransactions;
    protected readonly ILogger<CartActor> logger;

    private readonly Dictionary<string,List<CartItem>> history;

    public CartActor([PersistentState(
        stateName: "cart",
        storageName: Constants.OrleansStorage)] IPersistentState<Cart> state,
        AppConfig options,
        ILogger<CartActor> _logger)
    {
        this.cart = state;
        this.OrleansTransactions = options.OrleansTransactions;
        this.orleansStorage = options.OrleansStorage;
        this.trackHistory = options.TrackCartHistory;
        if(this.trackHistory) this.history = new Dictionary<string, List<CartItem>>();
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

    public Task<List<CartItem>> GetItems()
    {
        return Task.FromResult(this.cart.State.items);
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

        if(this.orleansStorage)
        {
            await this.cart.WriteStateAsync();
        }
    }

    // customer decided to checkout
    public virtual async Task NotifyCheckout(CustomerCheckout customerCheckout)
    {
        var checkout = new ReserveStock(DateTime.UtcNow, customerCheckout, this.cart.State.items, customerCheckout.instanceId);
        this.cart.State.status = CartStatus.CHECKOUT_SENT;
        try {
            if (this.trackHistory)
            {
                // store cart items internally
                this.history.TryAdd(customerCheckout.instanceId, new(this.cart.State.items));
            }
            if(OrleansTransactions)
            {
                await this.GrainFactory.GetGrain<ITransactionalOrderActor>(customerId).Checkout(checkout);
            }
            else
            {
                await this.GrainFactory.GetGrain<IOrderActor>(customerId).Checkout(checkout);
            }
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
        {
            await this.cart.WriteStateAsync();
        }
    }

    public Task<List<CartItem>> GetHistory(string tid)
    {
        if(this.history.TryGetValue(tid, out List<CartItem> value))
        {
            return Task.FromResult(value);
        }
        return Task.FromResult(new List<CartItem>());
    }

}