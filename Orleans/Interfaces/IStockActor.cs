using Common.Entities;
using Common.Events;

namespace Orleans.Interfaces;

public interface IStockActor : IGrainWithIntegerCompoundKey
{
    public Task<ItemStatus> AttemptReservation(CartItem cartItem);
    public Task CancelReservation(int quantity);
    public Task ConfirmReservation(int quantity);

    public Task ProcessProductUpdate(ProductUpdated productUpdated);

    public Task SetItem(StockItem item);
}