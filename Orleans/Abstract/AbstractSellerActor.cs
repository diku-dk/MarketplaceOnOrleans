using Common.Config;
using Common.Entities;
using Common.Events;
using Common.Integration;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using OrleansApp.Grains;
using OrleansApp.Infra;
using OrleansApp.Interfaces;

namespace Orleans.Abstract;

public abstract class AbstractSellerActor : Grain, ISellerActor
{
    protected static readonly string Name = typeof(SellerActor).FullName;

    protected readonly ILogger<SellerActor> logger;
    protected readonly IAuditLogger persistence;

    protected int sellerId;

    protected readonly IPersistentState<Seller> seller;
    protected readonly AppConfig config;

    public AbstractSellerActor(
        [PersistentState("seller", Constants.OrleansStorage)] IPersistentState<Seller> seller,
        IAuditLogger persistence,
        AppConfig options,
        ILogger<SellerActor> logger)
    {
        this.seller = seller;
        this.config = options;
        this.persistence = persistence;
        this.logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken token)
    {
        this.sellerId = (int)this.GetPrimaryKeyLong();
        return Task.CompletedTask;
    }

    public async Task SetSeller(Seller seller)
    {
        this.seller.State = seller;
        if (this.config.OrleansStorage)
            await this.seller.WriteStateAsync();
    }

    public Task<Seller> GetSeller()
    {
        return Task.FromResult(this.seller.State);
    }

    public Task ProcessNewInvoice(InvoiceIssued invoiceIssued)
    {        
        var orderEntries = new List<OrderEntry>();
        foreach (var item in invoiceIssued.items)
        {
            OrderEntry orderEntry = new()
            {
                customer_id = invoiceIssued.customer.CustomerId,
                order_id = item.order_id,
                seller_id = item.seller_id,
                // package_id = not known yet
                product_id = item.product_id,
                product_name = item.product_name,
                quantity = item.quantity,
                total_amount = item.total_amount,
                total_items = item.total_items,
                total_invoice = item.total_amount + item.freight_value,
                total_incentive = item.voucher,
                freight_value = item.freight_value,
                // shipment_date = not known yet
                // delivery_date = not known yet
                order_status = OrderStatus.INVOICED,
                unit_price = item.unit_price,
            };
            orderEntries.Add(orderEntry);
        }
        return this.ProcessNewOrderEntries(invoiceIssued, orderEntries);
    }

    protected abstract Task ProcessNewOrderEntries(InvoiceIssued invoiceIssued, List<OrderEntry> orderEntries);

    public abstract Task ProcessDeliveryNotification(DeliveryNotification deliveryNotification);

    public abstract Task ProcessPaymentConfirmed(PaymentConfirmed paymentConfirmed);

    public abstract Task ProcessPaymentFailed(PaymentFailed paymentFailed);

    public abstract Task ProcessShipmentNotification(ShipmentNotification shipmentNotification);

    public abstract Task<SellerDashboard> QueryDashboard();

    public async Task Reset()
    {
        this.seller.State = null;
        if (this.config.OrleansStorage)
            await this.seller.WriteStateAsync();
    }

}

