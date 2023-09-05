namespace Common.Requests
{
    /**
     * A sub-type of customer.
     * Ideally, address and credit card info may change across customer checkouts
     * Basket and Order does not need to know all internal data about customers
     */
    public class CustomerCheckout
    {
    
        public int CustomerId { get; set; }

        /**
        * Delivery address (could be different from customer's address)
        */
        public string FirstName { get; set; } 

        public string LastName { get; set; } 

        public string Street { get; set; } 

        public string Complement { get; set; } 

        public string City { get; set; } 

        public string State { get; set; } 

        public string ZipCode { get; set; } 

        /**
        * Payment type
        */
        public string PaymentType { get; set; } 

        /**
        * Credit or debit card
        */
        public string CardNumber { get; set; } 

        public string CardHolderName { get; set; } 

        public string CardExpiration { get; set; } 

        public string CardSecurityNumber { get; set; } 

        public string CardBrand { get; set; } 

        // if no credit card, must be 1
        public int Installments { get; set; } 

        public int instanceId { get; set; }

        public CustomerCheckout(){ }

    }
    
}