using Common.Entities;
using Common.Events;
using Orleans.Concurrency;

namespace Orleans.Interfaces;

public interface IStockActor : IGrainWithIntegerCompoundKey
{
    Task<ItemStatus> AttemptReservation(CartItem cartItem);

    Task CancelReservation(int quantity);

    Task ConfirmReservation(int quantity);

    Task ProcessProductUpdate(ProductUpdated productUpdated);

    Task SetItem(StockItem item);

    [ReadOnly]
    Task<StockItem> GetItem();

    Task Reset();
}