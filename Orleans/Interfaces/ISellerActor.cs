using Common.Entities;
using Common.Events;
using Common.Integration;
using Orleans.Concurrency;

namespace Orleans.Interfaces;

public interface ISellerActor : IGrainWithIntegerKey
{

    // if invoice fails, all subsequent fails
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

    Task<SellerDashboard> QueryDashboard();

    Task Reset();

}

