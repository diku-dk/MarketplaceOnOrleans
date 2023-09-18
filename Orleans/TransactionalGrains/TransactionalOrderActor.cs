using Common;
using Common.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.AbstractGrains;
using Orleans.Grains;
using Orleans.Infra;
using Orleans.Transactions.Abstractions;

namespace Orleans.TransactionalGrains;

public class TransactionalOrderActor : AbstractOrderActor
{

    private readonly ITransactionalState<Dictionary<int, OrderState>> orders;
    private readonly ITransactionalState<NextOrderState> nextOrderId;

    public TransactionalOrderActor(    
        [TransactionalState(stateName: "orders", storageName: Constants.OrleansStorage)] ITransactionalState<Dictionary<int,OrderState>> orders,
        [TransactionalState(stateName: "nextOrderId", storageName: Constants.OrleansStorage)] ITransactionalState<NextOrderState> nextOrderId,
        IOptions<AppConfig> config,
        ILogger<OrderActor> _logger) : base(config, _logger)
    {
        this.orders = orders;
        this.nextOrderId = nextOrderId;
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

    public override async Task<int> GetNextOrderId()
    {
        await this.nextOrderId.PerformUpdate( id => id = id.GetNextOrderId() );
        return (await this.nextOrderId.PerformRead(id => id.Value));
    }

    public override async Task<int> GetNumOrders()
    {
        return await this.orders.PerformRead(order => order.Count);
    }

    public override async Task<OrderState> GetOrderFromState(int orderId)
    {
        return await this.orders.PerformRead(order => order[orderId]);
    }

    public override async Task<List<Order>> GetOrders()
    {
          return await this.orders.PerformRead(order => order.Select(x=>x.Value.order).ToList());
    }

    public override Task<bool> OrderExists(int orderId)
    {
        return this.orders.PerformRead( id => id.ContainsKey(orderId) );
    }

    public override Task RemoveOrderFromState(int orderId)
    {
        return this.orders.PerformUpdate( id => { id.Remove(orderId); });
    }

    static readonly List<Task> empty = Array.Empty<Task>().ToList();
    public override List<Task> SpawnFullWriteStateAsync()
    {
        return empty;
    }

    public override Task SpawnWriteStateAsync()
    {
        return Task.CompletedTask;
    }

    public override Task UpdateOrderState(int orderId, OrderState value)
    {
        return this.orders.PerformUpdate( id => { id.Add(orderId, value); });
    }
}

