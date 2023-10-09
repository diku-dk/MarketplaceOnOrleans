using Common.Events;
using Orleans.Interfaces;

namespace Orleans.Transactional;

public interface ITransactionalPaymentActor : IPaymentActor
{
    [Transaction(TransactionOption.Join)]
    new Task ProcessPayment(InvoiceIssued invoiceIssued);

}

