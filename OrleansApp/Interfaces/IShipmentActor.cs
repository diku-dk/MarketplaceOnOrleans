using Common.Entities;
using Common.Events;
using Orleans.Concurrency;

namespace OrleansApp.Interfaces;

public interface IShipmentActor : IGrainWithIntegerKey
{
    [ReadOnly]
    Task<List<Shipment>> GetShipments(int customerId);

    Task ProcessShipment(PaymentConfirmed paymentConfirmed);

	Task UpdateShipment(string tid);

    Task UpdateShipment(string tid, ISet<(int customerId, int orderId, int sellerId)> entries);

    Task Reset();
}
