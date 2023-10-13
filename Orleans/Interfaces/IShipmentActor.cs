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

    Task Reset();
}
