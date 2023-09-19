using Common.Entities;
using Common.Events;

namespace Orleans.Interfaces;

public interface IShipmentActor : IGrainWithIntegerKey
{
    Task<List<Shipment>> GetShipments(int customerId);

    Task ProcessShipment(PaymentConfirmed paymentConfirmed);

	Task UpdateShipment(int tid);

    Task Reset();
}
