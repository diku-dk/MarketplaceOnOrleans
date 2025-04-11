using Common.Entities;
using Common.Events;
using Orleans.Concurrency;
using OrleansApp.Interfaces;

namespace OrleansApp.Transactional;

public interface ITransactionalStockActor : IStockActor
{

    [Transaction(TransactionOption.Create)]
    new Task SetItem(StockItem item);

    [ReadOnly]
    [Transaction(TransactionOption.Create)]
    new Task<StockItem> GetItem();

    [Transaction(TransactionOption.Join)]
    new Task<ItemStatus> AttemptReservation(CartItem cartItem);

    [Transaction(TransactionOption.Join)]
    new Task CancelReservation(int quantity);

    [Transaction(TransactionOption.Join)]
    new Task ConfirmReservation(int quantity);

    [Transaction(TransactionOption.Join)]
    new Task ProcessProductUpdate(ProductUpdated productUpdated);

    [Transaction(TransactionOption.Create)]
    new Task Reset();

}

