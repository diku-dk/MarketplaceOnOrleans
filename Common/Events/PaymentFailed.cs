using System;
using Common.Entities;
using Common.Integration;
using Common.Requests;

namespace Common.Events
{
    public record PaymentFailed
    (
        string status,
        CustomerCheckout customer,
        int orderId,
        IList<OrderItem> items,
        float totalAmount,
        int instanceId
    );
}

