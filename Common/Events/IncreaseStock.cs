using System;
using Common.Entities;

namespace Common.Events
{
    public record IncreaseStock
    (
        int seller_id,

        int product_id,

        int quantity

        
    );
}

