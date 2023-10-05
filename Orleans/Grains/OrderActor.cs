using Common.Entities;
using Microsoft.Extensions.Logging;
using Orleans.Infra;
using Orleans.Runtime;
using Orleans.AbstractGrains;
using Common;
using Microsoft.Extensions.Options;

namespace Orleans.Grains;

public sealed class OrderActor : AbstractOrderActor
{
    // Dictionary<int, (Order, List<OrderItem>)> orders;   // <order ID, order state, order item state>

    private readonly IPersistentState<Dictionary<int, OrderState>> orders;
    private readonly IPersistentState<NextOrderState> nextOrderId;

    public OrderActor(
        [PersistentState(stateName: "orders", storageName: Constants.OrleansStorage)] IPersistentState<Dictionary<int,OrderState>> orders,
        [PersistentState(stateName: "nextOrderId", storageName: Constants.OrleansStorage)] IPersistentState<NextOrderState> nextOrderId,
        IOptions<AppConfig> config,
        ILogger<OrderActor> _logger) : base(config, _logger)
    {
        this.orders = orders;
        this.nextOrderId = nextOrderId;
    }

    public override Task OnActivateAsync(CancellationToken token)
    {
        // persistence
        if(this.orders.State is null) this.orders.State = new();
        if(this.nextOrderId.State is null) this.nextOrderId.State = new();
        
        this.customerId = (int)this.GetPrimaryKeyLong();
        return Task.CompletedTask;
    }

    public override async Task TestTransaction(Order order)
    {
        OrderState orderState = new OrderState{
            order = order,
            orderItems = new(),
            orderHistory = new()
        };
        await UpdateOrderState(order.id, orderState);
 
    }

    public override Task<List<Order>> GetOrders()
    {
        var res = this.orders.State.Select(x=>x.Value.order).ToList();
        return Task.FromResult(res);
    }

    public override Task<int> GetNumOrders()
    {
        return Task.FromResult(this.nextOrderId.State.Value);
    }

    public override Task<int> GetNextOrderId()
    {
        this.nextOrderId.State = this.nextOrderId.State.GetNextOrderId();
        return Task.FromResult( this.nextOrderId.State.Value );
    }

    public override Task UpdateOrderState(int orderId, OrderState value)
    {
        this.orders.State.Add(orderId, value);
        return Task.WhenAll(
            nextOrderId.WriteStateAsync(),
            orders.WriteStateAsync()
        );
    }

    public override OrderState GetOrderFromState(int orderId)
    {
        return orders.State[orderId];
    }

    public override async Task RemoveOrderFromState(int orderId)
    {
        orders.State.Remove(orderId);
        await orders.WriteStateAsync();
    }
}
