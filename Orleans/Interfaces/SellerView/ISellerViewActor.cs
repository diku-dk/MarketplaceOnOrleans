using OrleansApp.Interfaces;
using Common.Events;
using Orleans.Concurrency;

namespace Orleans.Interfaces.SellerView;

/**
 * To have consistent seller dashboard, methods that alter the view state must not be one way
 */
public interface ISellerViewActor : ISellerActor
{

    new Task ProcessNewInvoice(InvoiceIssued invoiceIssued);

    new Task ProcessShipmentNotification(ShipmentNotification shipmentNotification);

}