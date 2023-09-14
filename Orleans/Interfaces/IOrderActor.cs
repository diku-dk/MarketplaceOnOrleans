using Common.Entities;
using Common.Events;
using Orleans.Concurrency;

namespace Orleans.Interfaces
{
    public interface IOrderActor : IGrainWithIntegerKey
    {
        Task Checkout(ReserveStock reserveStock);

        [OneWay]
        Task ProcessShipmentNotification(ShipmentNotification shipmentNotification);

        Task<List<Order>> GetOrders();

        Task<int> GetNumOrders();

    }
}