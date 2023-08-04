using System;
namespace Common.Integration
{
    /*
     * Idempotency key must go in the header:
     * Idempotency-Key: <key>
     * Source: https://stripe.com/docs/api/idempotent_requests
     * 
     */
    public class PaymentIntentCreateOptions {

        public string Customer { get; set; } = "";
        public float Amount { get; set; }
        public string PaymentMethod { get; set; } = "";
        public string IdempotencyKey { get; set; } = "";
        public CardOptions? cardOptions { get; set; }
        public string SetupFutureUsage { get; set; } = "off_session";
        public Currency Currency { get; set; } = Currency.USD;

    }
	
}

