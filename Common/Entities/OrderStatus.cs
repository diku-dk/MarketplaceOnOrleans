using System;
namespace Common.Entities
{
    /**
     * https://dev.olist.com/docs/orders
     */
    public enum OrderStatus
    {
        // few tuples in olist data set show this status
        // most of them have a respective payment line
        // which can be considered an anomaly (shold have been at least shipped?)
        // we assume some bug or error might have caused this discrepancy
        // status is not shown in dev olist
        // in this benchmark we use as the initial status on order object
        CREATED,
        /***
         * Given the separation of Marketplace and olist 
         * (https://dev.olist.com/docs/orders-notifications-details),
         * the status "processing" is used to flag the invoice
         * is ought to be emmitted from the seller.
         * In our case, we ignore this status because the payment
         * is being handled directly by our application as in
         * most e-commerce systems. In sum, we don't use this status
         */
        PROCESSING,
        APPROVED,
        CANCELED,
        // there is no mention about this status in dev olist
        // https://dev.olist.com/docs/orders
        // we assume it is the seller informing the system
        // about the unavailability of items
        // in or case we are handling the stock directly, so
        // this status is also not used (for now)
        UNAVAILABLE,
        // our concept of invoiced differs from what is prescribed
        // by olist use case. an invoice for our benchmark
        // is a request of payment. more info in order actor
        INVOICED,
        // generic term to address the order is on the way to the customer. fine grained tracking is provided by the shipment service
        READY_FOR_SHIPMENT,
        IN_TRANSIT,
        DELIVERED,

        // created for the benchmark
        PAYMENT_FAILED,
        PAYMENT_PROCESSED
    }
}

