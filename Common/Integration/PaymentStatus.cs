using System;
namespace Common.Integration
{
    /**
     * Based on Stripe API
     * https://stripe.com/docs/payments/intents#intent-statuses
     */
    public enum PaymentStatus
	{
        requires_payment_method, // aka failed
        succeeded,

        // not used for now. in the future, customers could cancel the order
        canceled

    }
}

