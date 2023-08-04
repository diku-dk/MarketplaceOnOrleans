using System;
using System.Collections.Generic;
using Common.Entities;
using Common.Requests;

namespace Common.Events
{
    public record StockConfirmed
    (
        DateTime timestamp,
        CustomerCheckout customerCheckout,
        List<CartItem> items,
        int instanceId
    );
}

