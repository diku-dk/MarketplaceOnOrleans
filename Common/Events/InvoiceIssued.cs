using Common.Entities;
using Common.Requests;

namespace Common.Events
{
    /*
     * "An invoice acts as a request for payment for the delivery of goods or services."
     * Source: https://invoice.2go.com/learn/invoices/invoice-vs-purchase-order/
     * An invoice data structure contains all necessary info for the payment 
     * actor to process a payment
     */
    public record InvoiceIssued
    (
        CustomerCheckout customer,
        int orderId,
        string invoiceNumber,
        DateTime issueDate,
        float totalInvoice,
        List<OrderItem> items,
        int instanceId
    );
}

