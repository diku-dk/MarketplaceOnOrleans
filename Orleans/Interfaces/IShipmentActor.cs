using Common.Entities;
using Common.Events;

namespace Orleans.Interfaces;

public interface IShipmentActor : IGrainWithIntegerKey
{

	Task ProcessShipment(PaymentConfirmed paymentConfirmed);

	Task UpdateShipment(int tid);

	// for test only
	Task<List<Shipment>> GetShipment(int customerId);
}
