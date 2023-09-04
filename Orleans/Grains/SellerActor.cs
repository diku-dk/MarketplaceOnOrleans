using Common.Events;
using Microsoft.Extensions.Logging;
using Orleans.Interfaces;

namespace Orleans.Grains;

	public class SellerActor : Grain, ISellerActor
{
    private readonly ILogger<SellerActor> _logger;

    public SellerActor(
        ILogger<SellerActor> _logger)
    {
        this._logger = _logger;
    }

    public Task IndexProduct(int product_id)
    {
        return Task.CompletedTask;
    }

    public Task ProcessNewInvoice(InvoiceIssued invoiceIssued)
    {
        throw new NotImplementedException();
    }

    public Task ProcessPaymentConfirmed(PaymentConfirmed paymentConfirmed)
    {
        throw new NotImplementedException();
    }

    public Task ProcessPaymentFailed(PaymentFailed paymentFailed)
    {
        throw new NotImplementedException();
    }

    public Task ProcessShipmentNotification(ShipmentNotification shipmentNotification)
    {
        throw new NotImplementedException();
    }
}

