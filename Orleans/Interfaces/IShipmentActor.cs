using System;
using Common.Events;

namespace Orleans.Interfaces;

public interface IShipmentActor
{


	void ProcessShipment(PaymentConfirmed paymentConfirmed);


}


