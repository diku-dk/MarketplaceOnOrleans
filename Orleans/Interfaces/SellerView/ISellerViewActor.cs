using OrleansApp.Interfaces;
using Common.Events;

namespace Orleans.Interfaces.SellerView;

/**
 * To have consistent seller dashboard, methods should not be one way
 */
public interface ISellerViewActor : ISellerActor
{

    new Task ProcessNewInvoice(InvoiceIssued invoiceIssued);

    new Task ProcessPaymentConfirmed(PaymentConfirmed paymentConfirmed);

    new Task ProcessPaymentFailed(PaymentFailed paymentFailed);

    new Task ProcessShipmentNotification(ShipmentNotification shipmentNotification);

    new Task ProcessDeliveryNotification(DeliveryNotification deliveryNotification);

}