using Common.Events;
using Orleans.Concurrency;

namespace Orleans.Interfaces;

public interface ISellerActor : IGrainWithIntegerKey
{
    public Task IndexProduct(int product_id);

    [OneWay]
    public Task ProcessNewInvoice(InvoiceIssued invoiceIssued);

    [OneWay]
    public Task ProcessPaymentConfirmed(PaymentConfirmed paymentConfirmed);

    [OneWay]
    public Task ProcessPaymentFailed(PaymentFailed paymentFailed);

    [OneWay]
    public Task ProcessShipmentNotification(ShipmentNotification shipmentNotification);

}


