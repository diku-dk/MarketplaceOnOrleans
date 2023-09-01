using Common.Events;

namespace Orleans.Interfaces;

public interface IPaymentActor : IGrainWithIntegerKey
{
	Task ProcessPayment(InvoiceIssued invoiceIssued);
}