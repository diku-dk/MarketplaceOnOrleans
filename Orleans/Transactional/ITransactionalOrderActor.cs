using Common.Entities;
using Common.Events;
using OrleansApp.Interfaces;
using System.Transactions;

namespace OrleansApp.Transactional;

public interface ITransactionalOrderActor : IOrderActor
{
    [Transaction(TransactionOption.Create)]
    new Task Checkout(ReserveStock reserveStock);

    [Transaction(TransactionOption.Join)]
    new Task ProcessPaymentConfirmed(PaymentConfirmed paymentConfirmed);

    [Transaction(TransactionOption.Join)]
    new Task ProcessPaymentFailed(PaymentFailed paymentFailed);

    [Transaction(TransactionOption.Join)]
    new Task ProcessShipmentNotification(ShipmentNotification shipmentNotification);

    [Transaction(TransactionOption.CreateOrJoin)]
    new Task<List<Order>> GetOrders();

    [Transaction(TransactionOption.CreateOrJoin)]
    new Task<int> GetNumOrders();

    [Transaction(TransactionOption.CreateOrJoin)]
    Task TestTransaction(Order order);

}
