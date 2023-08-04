using System;
using Common.Entities;
using Common.Integration;
using Common.Requests;

namespace Common.Events
{
    public record PaymentConfirmed
    (
        CustomerCheckout customer,
        int orderId,
        float totalAmount,
        IList<OrderItem> items,
        DateTime date,
        int instanceId
    );
}

