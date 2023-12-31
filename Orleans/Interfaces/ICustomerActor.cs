﻿using Common.Entities;
using Common.Events;
using Orleans.Concurrency;

namespace OrleansApp.Interfaces
{
    public interface ICustomerActor : IGrainWithIntegerKey
    {
        // API
        Task SetCustomer(Customer customer);

        Task Clear();

        [ReadOnly]
        Task<Customer> GetCustomer();

        [OneWay]
        Task NotifyPaymentConfirmed(PaymentConfirmed paymentConfirmed);

        [OneWay]
        Task NotifyPaymentFailed(PaymentFailed paymentFailed);

        [OneWay]
        Task NotifyDelivery(DeliveryNotification deliveryNotification);

    }
}
