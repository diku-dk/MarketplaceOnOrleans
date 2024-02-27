using OrleansApp.Interfaces;
using Common.Events;
using Orleans.Concurrency;

namespace Orleans.Interfaces.SellerView;

/**
 * To have strong consistent seller dashboard, methods that alter the view state should not be one way
 * However, there is a severe performance penalty compared to baseline (in-memory view maintenance)
 */
public interface ISellerViewActor : ISellerActor
{

    // new Task ProcessNewInvoice(InvoiceIssued invoiceIssued);

    // new Task ProcessShipmentNotification(ShipmentNotification shipmentNotification);

}