using Common.Entities;
using Microsoft.Extensions.Logging;
using OrleansApp.Infra;
using Orleans.Runtime;
using Common;
using Orleans.Concurrency;
using OrleansApp.Abstract;
using OrleansApp.Interfaces;

namespace OrleansApp.Grains;

[Reentrant]
public sealed class OrderActor : AbstractOrderActor
{

    // Dictionary<int, (Order, List<OrderItem>)> orders;   // <order ID, order state, order item state>

    private readonly IPersistentState<Dictionary<int, OrderState>> orders;
    private readonly IPersistentState<NextOrderIdState> nextOrderId;

    public OrderActor(
        [PersistentState(stateName: "orders", storageName: Constants.OrleansStorage)] IPersistentState<Dictionary<int,OrderState>> orders,
        [PersistentState(stateName: "nextOrderId", storageName: Constants.OrleansStorage)] IPersistentState<NextOrderIdState> nextOrderId,
        IPersistence persistence,
        AppConfig options,
        ILogger<OrderActor> _logger) : base(persistence, options, _logger)
    {
        this.orders = orders;
        this.nextOrderId = nextOrderId;
    }

    public override async Task OnActivateAsync(CancellationToken token)
    {
        // orleans storage
        if(this.orders.State is null) this.orders.State = new();
        if(this.nextOrderId.State is null) this.nextOrderId.State = new();
        await base.OnActivateAsync(token);
    }

    protected override Task<int> GetNextOrderId()
    {
        this.nextOrderId.State = this.nextOrderId.State.GetNextOrderId();
        return Task.FromResult(this.nextOrderId.State.Value);
    }

    public override Task<List<Order>> GetOrders()
    {
        var res = this.orders.State.Select(x=>x.Value.order).ToList();
        return Task.FromResult(res);
    }

    public override Task<int> GetNumOrders()
    {
        return Task.FromResult(this.orders.State.Count);
    }

    public override async Task Reset()
    {
        this.orders.State.Clear();
        if(config.OrleansStorage)
            await orders.WriteStateAsync();
    }

    public override OrderState GetOrderFromState(int orderId)
    {
        return orders.State[orderId];
    }

    public override async Task RemoveOrderFromState(int orderId)
    {
        orders.State.Remove(orderId);
        if (config.OrleansStorage)
            await orders.WriteStateAsync();
    }

    public override Task InsertOrderIntoState(int orderId, OrderState value)
    {
        this.orders.State.Add(orderId, value);
        return Task.WhenAll(
            nextOrderId.WriteStateAsync(),
            orders.WriteStateAsync()
        );
    }

    public override async Task UpdateOrderState(int orderId, OrderState value)
    {
        this.orders.State[orderId] = value;
        if (config.OrleansStorage)
        {
            // possible solution for reentrancy: https://github.com/dotnet/orleans/issues/4697#issuecomment-398556401
            // await nextOrderId.ReadStateAsync().ContinueWith(x=>nextOrderId.WriteStateAsync());
            await orders.WriteStateAsync();
        }
    }

    public override ISellerActor GetSellerActor(int sellerId)
    {
        return this.GrainFactory.GetGrain<ISellerActor>(sellerId, "Orleans.Grains.SellerActor");
    }

    public override IStockActor GetStockActor(int sellerId, int productId)
    {
        return this.GrainFactory.GetGrain<IStockActor>(sellerId, productId.ToString(), "Orleans.Grains.StockActor");
    }

    public override IPaymentActor GetPaymentActor(int customerId)
    {
        return this.GrainFactory.GetGrain<IPaymentActor>(customerId, "Orleans.Grains.PaymentActor");
    }
}
