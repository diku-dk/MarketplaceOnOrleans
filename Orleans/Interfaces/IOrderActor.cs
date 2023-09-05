using Common.Events;
using Orleans.Concurrency;

namespace Orleans.Interfaces
{
    public interface IOrderActor : IGrainWithIntegerKey
    {
        [OneWay]
        Task Checkout(ReserveStock reserveStock);

        [OneWay]
        Task ProcessShipmentNotification(ShipmentNotification shipmentNotification);

    }
}