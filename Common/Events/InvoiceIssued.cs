using Common.Entities;
using Common.Requests;
using Newtonsoft.Json;

namespace Common.Events;
/*
    * "An invoice acts as a request for payment for the delivery of goods or services."
    * Source: https://invoice.2go.com/learn/invoices/invoice-vs-purchase-order/
    * An invoice data structure contains all necessary info for the payment 
    * actor to process a payment
    */
public class InvoiceIssued
{
    //[JsonProperty("customer")]
    public CustomerCheckout customer { get; set; }

    public int orderId { get; set; }

    public string invoiceNumber { get; set; }

    public DateTime issueDate { get; set; }

    public float totalInvoice { get; set; }

    public List<OrderItem> items { get; set; }

    public string instanceId { get; set; }

    public InvoiceIssued(){}

    public InvoiceIssued(CustomerCheckout customer, int orderId, string invoiceNumber, DateTime issueDate, float totalInvoice, List<OrderItem> items, string instanceId)
    {
        this.customer = customer;
        this.orderId = orderId;
        this.invoiceNumber = invoiceNumber;
        this.issueDate = issueDate;
        this.totalInvoice = totalInvoice;
        this.items = items;
        this.instanceId = instanceId;
    }
}

