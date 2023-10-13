using Common.Entities;
using Common.Events;
using Orleans.Concurrency;
using OrleansApp.Interfaces;

namespace OrleansApp.Transactional;

public interface ITransactionalShipmentActor : IShipmentActor
{
    [ReadOnly]
    [Transaction(TransactionOption.CreateOrJoin)]
    new Task<List<Shipment>> GetShipments(int customerId);

    [Transaction(TransactionOption.Join)]
    new Task ProcessShipment(PaymentConfirmed paymentConfirmed);

    [Transaction(TransactionOption.Create)]
    new Task UpdateShipment(string tid);

    [Transaction(TransactionOption.Create)]
    new Task Reset();

}

