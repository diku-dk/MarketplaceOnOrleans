using System;
using Common.Integration;

namespace Common.Entities
{
	public class OrderPayment
	{
        public int order_id { get; set; }

        // 1 - coupon, 2 - coupon, 3 - credit card
        public int payment_sequential { get; set; }

        // coupon, credit card
        public PaymentType type { get; set; }

        // number of times the credit card is charged (usually once a month)
        public int installments { get; set; }

        // respective to this line (ie. coupon)
        public float value { get; set; }

        public PaymentStatus? status { get; set; }
    }
}

