using Common.Entities;
using Common.Events;
using Orleans.Concurrency;

namespace OrleansApp.Interfaces
{
    public interface IOrderActor : IGrainWithIntegerKey
    {
        Task Checkout(ReserveStock reserveStock);

        [OneWay]
        Task ProcessPaymentConfirmed(PaymentConfirmed paymentConfirmed);

        [OneWay]
        Task ProcessPaymentFailed(PaymentFailed paymentFailed);

        [OneWay]
        Task ProcessShipmentNotification(ShipmentNotification shipmentNotification);

        [ReadOnly]
        Task<List<Order>> GetOrders();

        [ReadOnly]
        Task<int> GetNumOrders();

        Task Reset();

    }
}