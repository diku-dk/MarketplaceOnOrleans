using Common.Entities;
using Common.Events;

namespace Orleans.Interfaces
{
    public interface IStockActor : IGrainWithIntegerCompoundKey
    {
        public Task<ItemStatus> AttemptReservation(int quantity);
        public Task CancelReservation(int quantity);
        public Task ConfirmReservation(int quantity);


        void ProcessPayment(PaymentConfirmed paymentConfirmed);
        void ProcessPayment(PaymentFailed paymentFailed);

        public Task DeleteItem();

        public Task SetItem(StockItem item);

    }
}
