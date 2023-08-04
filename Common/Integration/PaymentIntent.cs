using System;
namespace Common.Integration
{
	/**
     * Inspired by Stripe payment system
	 * https://stripe.com/docs/api/payment_intents
	 * https://stripe.com/docs/payments/payment-intents
	 * 
	 * https://stripe.com/docs/payments/quickstart?lang=dotnet&client=java&platform=android
	 * "A PaymentIntent tracks the customer’s payment lifecycle{ get; set; } 
	 * keeping track of any failed payment attempts and ensuring 
	 * the customer is only charged once. Return the PaymentIntent’s 
	 * client secret in the response to finish the payment on the client."
	 * 
	 * "We recommend that you create exactly one PaymentIntent for each order 
	 * or customer session in your system. You can reference the PaymentIntent 
	 * later to see the history of payment attempts for a particular session."
	 */
	public class PaymentIntent
	{
		// example: pi_1GszdL2eZvKYlo2C4nORvwio
		public string id { get; set; } = "";

        public float amount { get; set; }

        // https://stripe.com/docs/api/errors#errors-setup_intent-status
        public string status { get; set; } = PaymentStatus.succeeded.ToString();

        // example: pi_1GszdL2eZvKYlo2C4nORvwio_secret_F06b3J3jgLq8Ueo5JeZUF79mr
        public string client_secret { get; set; } = "";

        public string currency { get; set; } = "";

        public string customer { get; set; } = "";

		public string confirmation_method = "automatic";

        public long created { get; set; }

	}
}

