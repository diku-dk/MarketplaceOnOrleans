using System;
using Common.Events;

namespace Orleans.Interfaces
{
	public interface IPaymentActor
	{

		void ProcessPayment(InvoiceIssued invoiceIssued);

	}
}

