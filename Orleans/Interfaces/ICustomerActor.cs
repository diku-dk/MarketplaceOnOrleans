using Common.Entities;
using Common.Events;

namespace Orleans.Interfaces
{
    public interface ICustomerActor : IGrainWithIntegerKey
    {
        // API
        Task SetCustomer(Customer customer);
        Task Clear();
        Task<Customer> GetCustomer();

        Task NotifyPaymentConfirmed(PaymentConfirmed paymentConfirmed);
        Task NotifyPaymentFailed(PaymentFailed paymentFailed);
        Task NotifyDelivery(DeliveryNotification deliveryNotification);

    }
}
