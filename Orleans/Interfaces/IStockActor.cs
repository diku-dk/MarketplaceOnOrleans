using Common.Entities;

namespace Orleans.Interfaces
{
    public interface IStockActor : IGrainWithIntegerCompoundKey
    {
        public Task<ItemStatus> AttemptReservation(int quantity);
        public Task CancelReservation(int quantity);
        public Task ConfirmReservation(int quantity);

        public Task DeleteItem();

        public Task SetItem(StockItem item);

    }
}
