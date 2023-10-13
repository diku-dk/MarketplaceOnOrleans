using System.Text;
using Common.Entities;
using Common.Events;
using Common.Integration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using OrleansApp.Interfaces;
using Orleans.Runtime;
using OrleansApp.Infra;
using Common;
using Orleans.Concurrency;

namespace OrleansApp.Grains;

[Reentrant]
public sealed class SellerActor : Grain, ISellerActor
{

    private readonly ILogger<SellerActor> logger;
    private readonly IPersistence persistence;

    private int sellerId;

    private readonly IPersistentState<Seller> seller;
    private readonly IPersistentState<Dictionary<string, List<OrderEntry>>> orderEntries;
    private readonly AppConfig config;

    public SellerActor(
        [PersistentState(stateName: "seller", storageName: Constants.OrleansStorage)] IPersistentState<Seller> seller,
        [PersistentState(stateName: "orderEntries", storageName: Constants.OrleansStorage)] IPersistentState<Dictionary<string, List<OrderEntry>>> orderEntries,
        IPersistence persistence,
        AppConfig options,
        ILogger<SellerActor> logger)
    {
        this.seller = seller;
        this.orderEntries = orderEntries;
        this.config = options;
        this.persistence = persistence;
        this.logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken token)
    {
        this.sellerId = (int) this.GetPrimaryKeyLong();

        // persistence
        if(this.orderEntries.State is null) this.orderEntries.State = new();

        return Task.CompletedTask;
    }

    public async Task SetSeller(Seller seller)
    {
        this.seller.State = seller;
        if(this.config.OrleansStorage)
            await this.seller.WriteStateAsync();
    }

    private static string BuildUniqueOrderIdentifier(InvoiceIssued invoiceIssued)
    {
        return new StringBuilder(invoiceIssued.customer.CustomerId.ToString()).Append('-').Append(invoiceIssued.orderId).ToString();
    }

    private static string BuildUniqueOrderIdentifier(PaymentConfirmed paymentConfirmed)
    {
        return new StringBuilder(paymentConfirmed.customer.CustomerId.ToString()).Append('-').Append(paymentConfirmed.orderId).ToString();
    }

    private static string BuildUniqueOrderIdentifier(PaymentFailed paymentFailed)
    {
        return new StringBuilder(paymentFailed.customer.CustomerId.ToString()).Append('-').Append(paymentFailed.orderId).ToString();
    }

    private static string BuildUniqueOrderIdentifier(ShipmentNotification shipmentNotification)
    {
        return new StringBuilder(shipmentNotification.customerId.ToString()).Append('-').Append(shipmentNotification.orderId).ToString();
    }

    private static string BuildUniqueOrderIdentifier(DeliveryNotification deliveryNotification)
    {
        return new StringBuilder(deliveryNotification.customerId.ToString()).Append('-').Append(deliveryNotification.orderId).ToString();
    }

    public async Task ProcessNewInvoice(InvoiceIssued invoiceIssued)
    {
        string id = BuildUniqueOrderIdentifier(invoiceIssued);
        var orderEntries = new List<OrderEntry>();
        var added =  this.orderEntries.State.TryAdd(id, orderEntries);
        if(!added){
            logger.LogError("Seller {0} - Customer ID {1} Order ID {2} already exists. {3},{4}", this.sellerId, invoiceIssued.customer.CustomerId, invoiceIssued.orderId, invoiceIssued.items[0].order_id, this.orderEntries.State[id][0].order_id);
            return;
        }

        foreach (var item in invoiceIssued.items)
        {
            OrderEntry orderEntry = new()
            {
                order_id = id,
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

        if(this.config.OrleansStorage){
            await this.orderEntries.WriteStateAsync();
        }
    }

    public async Task ProcessPaymentConfirmed(PaymentConfirmed paymentConfirmed)
    {
        string id = BuildUniqueOrderIdentifier(paymentConfirmed);
        if(!this.orderEntries.State.ContainsKey(id)) {
            logger.LogDebug("Cannot process payment confirmed event because invoice ID {0} has not been found", id);
            return; // Have been either removed from state already or not yet added to the state (due to interleaving)
        }
        foreach (var item in this.orderEntries.State[id])
        {
            item.order_status = OrderStatus.PAYMENT_PROCESSED;
        }
        if(this.config.OrleansStorage)
            await this.orderEntries.WriteStateAsync();
    }

    public async Task ProcessPaymentFailed(PaymentFailed paymentFailed)
    {
        string id = BuildUniqueOrderIdentifier(paymentFailed);
        if(!this.orderEntries.State.ContainsKey(id)) return;
        foreach (var item in this.orderEntries.State[id])
        {
            item.order_status = OrderStatus.PAYMENT_FAILED;
        }
        if(this.config.OrleansStorage)
            await this.orderEntries.WriteStateAsync();
    }

    public async Task ProcessShipmentNotification(ShipmentNotification shipmentNotification)
    {
        string id = BuildUniqueOrderIdentifier(shipmentNotification);
        if (!this.orderEntries.State.ContainsKey(id))
        {
            logger.LogDebug("Cannot process shipment notification event because invoice ID {0} has not been found", id);
            return; // Have been either removed from state already or not yet added to the state (due to interleaving)
        }
        foreach (var item in this.orderEntries.State[id])
        {
            if(shipmentNotification.status == ShipmentStatus.approved){
                item.order_status = OrderStatus.READY_FOR_SHIPMENT;
                item.shipment_date = shipmentNotification.eventDate;
                item.delivery_status = PackageStatus.ready_to_ship;
            }
            if(shipmentNotification.status == ShipmentStatus.delivery_in_progress){
                item.order_status = OrderStatus.IN_TRANSIT;
                item.delivery_status = PackageStatus.shipped;
            }
            if(shipmentNotification.status == ShipmentStatus.concluded){
                item.order_status = OrderStatus.DELIVERED;
            }
                
        }
        // log delivered entries and remove them from state
        if (shipmentNotification.status == ShipmentStatus.concluded)
        {
            List<OrderEntry> entries = this.orderEntries.State[id];
            if(this.config.LogRecords){
                var str = JsonSerializer.Serialize(entries);
                await persistence.Log(Name, id, str);
            }
            this.orderEntries.State.Remove(id);
        }
        if(this.config.OrleansStorage)
            await this.orderEntries.WriteStateAsync();
    }

    private static readonly string Name = typeof(SellerActor).FullName;

    public async Task ProcessDeliveryNotification(DeliveryNotification deliveryNotification)
    {
        string id = BuildUniqueOrderIdentifier(deliveryNotification);
        // interleaving of shipment and delivery
        if (!this.orderEntries.State.ContainsKey(id))
        {
            logger.LogDebug("Cannot process delivery notification event because invoice ID {0} has not been found", id);
            return;
        }
        var entry = this.orderEntries.State[id].FirstOrDefault(oe=>oe.product_id == deliveryNotification.productId, null);
        if(entry is not null)
        {
            entry.package_id = deliveryNotification.packageId;
            entry.delivery_status = PackageStatus.delivered;
            entry.delivery_date = deliveryNotification.deliveryDate;
            if(this.config.OrleansStorage)
                await this.orderEntries.WriteStateAsync();
        }
    }

    public Task<SellerDashboard> QueryDashboard()
    {
        // Queries not present in Orleans: https://github.com/dotnet/orleans/issues/4232
        var entries = this.orderEntries.State.SelectMany(x=>x.Value).ToList();
        OrderSellerView view = new OrderSellerView()
        {
            seller_id = this.sellerId,
            count_orders = entries.Select(x=>x.order_id).ToHashSet().Count,
            count_items = entries.Count(),
            total_invoice = entries.Sum(x=>x.total_invoice),
            total_amount = entries.Sum(x=>x.total_amount),
            total_freight = entries.Sum(x=>x.freight_value),
            total_incentive = entries.Sum(x=>x.total_incentive),
            total_items = entries.Sum(x=>x.total_items),
            
        };
        return Task.FromResult(new SellerDashboard(view,entries));
    }

    public async Task Reset()
    {
        this.orderEntries.State.Clear();
        if(this.config.OrleansStorage)
            await this.orderEntries.WriteStateAsync();
    }

    public Task<Seller> GetSeller()
    {
        return Task.FromResult(this.seller.State);
    }
}
