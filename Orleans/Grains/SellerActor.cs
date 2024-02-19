using Common.Entities;
using Common.Events;
using Common.Integration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Orleans.Runtime;
using OrleansApp.Infra;
using Orleans.Concurrency;
using Orleans.Abstract;
using System.Text;
using Common.Config;

namespace OrleansApp.Grains;

/**
 * This actor should not be used when Orleans Transactions option is set
 * due to interleaving problems related to ETag (writes to storage are always async)
 */
[Reentrant]
public sealed class SellerActor : AbstractSellerActor
{

    private readonly IPersistentState<Dictionary<string, List<OrderEntry>>> orderEntries;

    public SellerActor(
        [PersistentState(stateName: "seller", storageName: Constants.OrleansStorage)] IPersistentState<Seller> seller,
        [PersistentState(stateName: "orderEntries", storageName: Constants.OrleansStorage)] IPersistentState<Dictionary<string, List<OrderEntry>>> orderEntries,
        //IAuditLogger persistence,
        AppConfig options,
        //ILogger<SellerActor> logger) : base(seller, persistence, options, logger)
        ILogger<SellerActor> logger) : base(seller, options, logger)
    {
        this.orderEntries = orderEntries;
    }

    public override async Task OnActivateAsync(CancellationToken token)
    {
        await base.OnActivateAsync(token);
        // persistence
        this.orderEntries.State ??= new();
    }

    protected override async Task ProcessNewOrderEntries(InvoiceIssued invoiceIssued, List<OrderEntry> orderEntries)
    {
        string id = BuildUniqueOrderIdentifier(invoiceIssued);
        var containsKey = this.orderEntries.State.ContainsKey(id);
        if (containsKey)
        {
            this.logger.LogError("Seller {0} - Customer ID {1} Order ID {2} already exists. {3},{4}", this.sellerId, invoiceIssued.customer.CustomerId, invoiceIssued.orderId, invoiceIssued.items[0].order_id, this.orderEntries.State[id][0].order_id);
            return;
        }

        this.orderEntries.State.Add(id, orderEntries);

        if (this.config.OrleansStorage)
        {
            await this.orderEntries.WriteStateAsync();
        }
    }

    public override async Task ProcessPaymentConfirmed(PaymentConfirmed paymentConfirmed)
    {
        string id = BuildUniqueOrderIdentifier(paymentConfirmed);
        if (!this.orderEntries.State.ContainsKey(id))
        {
            logger.LogDebug("Cannot process payment confirmed event because invoice ID {0} has not been found", id);
            return; // Have been either removed from state already or not yet added to the state (due to interleaving)
        }
        foreach (var item in this.orderEntries.State[id])
        {
            item.order_status = OrderStatus.PAYMENT_PROCESSED;
        }
        if (this.config.OrleansStorage)
            await this.orderEntries.WriteStateAsync();
    }

    public override async Task ProcessPaymentFailed(PaymentFailed paymentFailed)
    {
        string id = BuildUniqueOrderIdentifier(paymentFailed);
        if (!this.orderEntries.State.ContainsKey(id)) return;
        foreach (var item in this.orderEntries.State[id])
        {
            item.order_status = OrderStatus.PAYMENT_FAILED;
        }
        if (this.config.OrleansStorage)
            await this.orderEntries.WriteStateAsync();
    }

    public override async Task ProcessShipmentNotification(ShipmentNotification shipmentNotification)
    {
        string id = BuildUniqueOrderIdentifier(shipmentNotification);
        if (!this.orderEntries.State.ContainsKey(id))
        {
            this.logger.LogDebug("Cannot process shipment notification event because invoice ID {0} has not been found", id);
            return; // Have been either removed from state already or not yet added to the state (due to interleaving)
        }

        // log delivered entries and remove them from state
        if (shipmentNotification.status == ShipmentStatus.concluded)
        {
            List<OrderEntry> entries = this.orderEntries.State[id];
            if (this.config.LogRecords)
            {
                var str = JsonSerializer.Serialize(entries);
                //await persistence.Log(Name, id, str);
            }
            this.orderEntries.State.Remove(id);
        }
        else
        {
            foreach (var item in this.orderEntries.State[id])
            {
                if (shipmentNotification.status == ShipmentStatus.approved)
                {
                    item.order_status = OrderStatus.READY_FOR_SHIPMENT;
                    item.shipment_date = shipmentNotification.eventDate;
                    item.delivery_status = PackageStatus.ready_to_ship;
                }
                if (shipmentNotification.status == ShipmentStatus.delivery_in_progress)
                {
                    item.order_status = OrderStatus.IN_TRANSIT;
                    item.delivery_status = PackageStatus.shipped;
                }

                /*
                if (shipmentNotification.status == ShipmentStatus.concluded) {
                    item.order_status = OrderStatus.DELIVERED;
                }
                */
            }
        }

        if (this.config.OrleansStorage)
            await this.orderEntries.WriteStateAsync();
    }

    public override async Task ProcessDeliveryNotification(DeliveryNotification deliveryNotification)
    {
        string id = BuildUniqueOrderIdentifier(deliveryNotification);
        // interleaving of shipment and delivery
        if (!this.orderEntries.State.ContainsKey(id))
        {
            this.logger.LogDebug("Cannot process delivery notification event because invoice ID {0} has not been found", id);
            return;
        }
        var entry = this.orderEntries.State[id].FirstOrDefault(oe => oe.product_id == deliveryNotification.productId, null);
        if (entry is not null)
        {
            entry.package_id = deliveryNotification.packageId;
            entry.delivery_status = PackageStatus.delivered;
            entry.delivery_date = deliveryNotification.deliveryDate;
            if (this.config.OrleansStorage)
                await this.orderEntries.WriteStateAsync();
        }
    }

    public override Task<SellerDashboard> QueryDashboard()
    {
        // Queries not present in Orleans: https://github.com/dotnet/orleans/issues/4232
        var entries = this.orderEntries.State.SelectMany(x => x.Value).ToList();
        OrderSellerView view = new OrderSellerView()
        {
            seller_id = this.sellerId,
            count_orders = entries.Select(x => x.order_id).ToHashSet().Count,
            count_items = entries.Count(),
            total_invoice = entries.Sum(x => x.total_invoice),
            total_amount = entries.Sum(x => x.total_amount),
            total_freight = entries.Sum(x => x.freight_value),
            total_incentive = entries.Sum(x => x.total_incentive),
            total_items = entries.Sum(x => x.total_items),

        };
        return Task.FromResult(new SellerDashboard(view, entries));
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

}