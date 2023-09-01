using Common.Entities;
using Common.Events;
using Orleans.Concurrency;

namespace Orleans.Interfaces
{
    public interface IStockActor : IGrainWithIntegerCompoundKey
    {
        public Task<ItemStatus> AttemptReservation(int quantity);
        public Task CancelReservation(int quantity);

        [OneWay]
        public Task ConfirmReservation(int quantity);


        void ProcessPayment(PaymentConfirmed paymentConfirmed);
        void ProcessPayment(PaymentFailed paymentFailed);

        public Task DeleteItem();

        public Task SetItem(StockItem item);

    }
}
