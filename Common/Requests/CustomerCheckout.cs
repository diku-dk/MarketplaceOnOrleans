namespace Common.Requests
{
    /**
     * A sub-type of customer.
     * Ideally, address and credit card info may change across customer checkouts
     * Basket and Order does not need to know all internal data about customers
     */
    public record CustomerCheckout(
    
        int CustomerId,

        /**
        * Delivery address (could be different from customer's address)
        */
        string FirstName,

        string LastName,

        string Street,

        string Complement,

        string City,

        string State,

        string ZipCode,

        /**
        * Payment type
        */
        string PaymentType,

        /**
        * Credit or debit card
        */
        string CardNumber,

        string CardHolderName,

        string CardExpiration,

        string CardSecurityNumber,

        string CardBrand,

        // if no credit card, must be 1
        int Installments,

        int instanceId
    );
    
}