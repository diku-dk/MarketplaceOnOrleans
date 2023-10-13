using Common.Events;

namespace OrleansApp.Interfaces;

public interface IPaymentActor : IGrainWithIntegerKey
{
	Task ProcessPayment(InvoiceIssued invoiceIssued);
}