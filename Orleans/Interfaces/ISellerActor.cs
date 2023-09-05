using Common.Entities;
using Common.Events;
using Orleans.Concurrency;

namespace Orleans.Interfaces;

public interface ISellerActor : IGrainWithIntegerKey
{

    Task IndexProduct(int product_id);

    [OneWay]
    Task ProcessNewInvoice(InvoiceIssued invoiceIssued);

    [OneWay]
    Task ProcessPaymentConfirmed(PaymentConfirmed paymentConfirmed);

    [OneWay]
    Task ProcessPaymentFailed(PaymentFailed paymentFailed);

    [OneWay]
    Task ProcessShipmentNotification(ShipmentNotification shipmentNotification);

    [OneWay]
    Task ProcessDeliveryNotification(DeliveryNotification deliveryNotification);

    Task SetSeller(Seller seller);

}


