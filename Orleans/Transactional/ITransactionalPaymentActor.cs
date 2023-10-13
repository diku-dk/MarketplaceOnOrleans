using Common.Events;
using OrleansApp.Interfaces;

namespace OrleansApp.Transactional;

public interface ITransactionalPaymentActor : IPaymentActor
{
    [Transaction(TransactionOption.Join)]
    new Task ProcessPayment(InvoiceIssued invoiceIssued);

}

