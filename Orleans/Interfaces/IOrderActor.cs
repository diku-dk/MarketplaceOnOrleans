using Common.Entities;
using Common.Events;
using Orleans.Concurrency;

namespace Orleans.Interfaces
{
    public interface IOrderActor : IGrainWithIntegerKey
    {
        [Transaction(TransactionOption.Join)]
        Task Checkout(ReserveStock reserveStock);

        [OneWay]
        [Transaction(TransactionOption.Join)]
        Task ProcessShipmentNotification(ShipmentNotification shipmentNotification);

        [Transaction(TransactionOption.CreateOrJoin)]
        Task<List<Order>> GetOrders();

        [Transaction(TransactionOption.CreateOrJoin)]
        Task<int> GetNumOrders();

        [Transaction(TransactionOption.Create)]
        Task TestTransaction(Order order);
    }
}