using Common.Events;

namespace Orleans.Interfaces;

public interface IShipmentActor : IGrainWithIntegerKey
{

	Task ProcessShipment(PaymentConfirmed paymentConfirmed);

	Task UpdateShipment();

}
