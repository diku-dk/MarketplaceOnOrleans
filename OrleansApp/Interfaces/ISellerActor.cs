using Common.Entities;
using Common.Events;
using Common.Integration;
using Orleans.Concurrency;

namespace OrleansApp.Interfaces;

public interface ISellerActor : IGrainWithIntegerKey
{

    // if invoice fails, all subsequent fails
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

    [ReadOnly]
    Task<Seller> GetSeller();

    [ReadOnly]
    Task<SellerDashboard> QueryDashboard();

    Task Reset();

}

