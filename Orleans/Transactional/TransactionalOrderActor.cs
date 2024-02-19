﻿using Common.Config;
using Common.Entities;
using Microsoft.Extensions.Logging;
using OrleansApp.Abstract;
using OrleansApp.Infra;
using OrleansApp.Interfaces;
using Orleans.Transactions.Abstractions;

namespace OrleansApp.Transactional;

public sealed class TransactionalOrderActor : AbstractOrderActor, ITransactionalOrderActor
{
    private readonly ITransactionalState<Dictionary<int, OrderState>> orders;
    private readonly ITransactionalState<NextOrderIdState> nextOrderId;

    public TransactionalOrderActor(
        [TransactionalState(stateName: "orders", storageName: Constants.OrleansStorage)] ITransactionalState<Dictionary<int, OrderState>> orders,
        [TransactionalState(stateName: "nextOrderId", storageName: Constants.OrleansStorage)] ITransactionalState<NextOrderIdState> nextOrderId,
        IPersistence persistence,
        AppConfig config,
        ILogger<TransactionalOrderActor> _logger) : base(persistence, config, _logger)
    {
        this.orders = orders;
        this.nextOrderId = nextOrderId;
    }

    public async Task TestTransaction(Order order)
    {
        OrderState orderState = new OrderState
        {
            order = order,
            orderItems = new(),
            orderHistory = new()
        };
        await InsertOrderIntoState(order.id, orderState);
    }

    public override ISellerActor GetSellerActor(int sellerId)
    {
        return GrainFactory.GetGrain<ISellerActor>(sellerId);
    }

    public override IStockActor GetStockActor(int sellerId, int productId)
    {
        return this.GrainFactory.GetGrain<ITransactionalStockActor>(sellerId, productId.ToString());
    }

    public override IPaymentActor GetPaymentActor(int customerId)
    {
        return this.GrainFactory.GetGrain<ITransactionalPaymentActor>(customerId);
    }

    protected override async Task<int> GetNextOrderId()
    {
         return await this.nextOrderId.PerformUpdate(id => id.GetNextOrderId().Value);
    }

    public override async Task<int> GetNumOrders()
    {
        return await this.orders.PerformRead(order => order.Count);
    }

    public override async Task<OrderState> GetOrderFromStateAsync(int orderId)
    {
        return await this.orders.PerformRead(order => {

            if (!order.ContainsKey(orderId))
            {
                  throw new InvalidOperationException(
                    $"Order ID {orderId} not found in order actor {this.customerId}.");
            }
            return order[orderId];
        });
    }

    public override Task<List<Order>> GetOrders()
    {
        return this.orders.PerformRead(order => order.Select(x => x.Value.order).ToList());
    }

    public override Task RemoveOrderFromState(int orderId)
    {
        return this.orders.PerformUpdate(id => { id.Remove(orderId); });
    }

    public override Task InsertOrderIntoState(int orderId, OrderState value)
    {
        return this.orders.PerformUpdate(id => { id.Add(orderId, value); });
    }

    public override Task Reset()
    {
        return this.orders.PerformUpdate(dic => { dic.Clear(); });
    }

    public override Task UpdateOrderState(int orderId, OrderState value)
    {
        return this.orders.PerformUpdate(id => {
            id[orderId].order.status = value.order.status;
            id[orderId].order.updated_at = value.order.updated_at;
            id[orderId].orderHistory.Add(value.orderHistory.Last());
        });
    }
}
