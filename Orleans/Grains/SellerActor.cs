using System.Text;
using Common.Entities;
using Common.Events;
using Common.Integration;
using Microsoft.Extensions.Logging;
using Orleans.Interfaces;
using Orleans.Runtime;

namespace Orleans.Grains;

public class SellerActor : Grain, ISellerActor
{
    private readonly ILogger<SellerActor> logger;

    private int sellerId;

    private readonly IPersistentState<Seller> seller;
    private readonly IPersistentState<Dictionary<string, List<OrderEntry>>> orderEntries;

    private readonly List<int> productIds = new List<int>();

    public SellerActor(
        [PersistentState(stateName: "seller", storageName: Infra.Constants.OrleansStorage)] IPersistentState<Seller> seller,
        [PersistentState(stateName: "orderEntries", storageName: Infra.Constants.OrleansStorage)] IPersistentState<Dictionary<string, List<OrderEntry>>> orderEntries,
        ILogger<SellerActor> logger)
    {
        this.seller = seller;
        this.orderEntries = orderEntries;
        this.logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken token)
    {
        this.sellerId = (int) this.GetPrimaryKeyLong();
        if(this.orderEntries.State is null) this.orderEntries.State = new();
        return Task.CompletedTask;
    }

    // only in case there is interactive product query
    public Task IndexProduct(int productId)
    {
        this.productIds.Add(productId);
        return Task.CompletedTask;
    }

    public async Task SetSeller(Seller seller)
    {
        this.seller.State = seller;
        await this.seller.WriteStateAsync();
    }

    public async Task ProcessNewInvoice(InvoiceIssued invoiceIssued)
    {
        string id = new StringBuilder(invoiceIssued.customer.CustomerId).Append("-").Append(invoiceIssued.orderId).ToString();
        this.orderEntries.State.Add(id, new List<OrderEntry>());
        foreach (var item in invoiceIssued.items.Where(x=>x.seller_id == this.sellerId))
        {
            OrderEntry orderEntry = new()
            {
                order_id = invoiceIssued.orderId,
                seller_id = item.seller_id,
                // package_id = not known yet
                product_id = item.product_id,
                product_name = item.product_name,
                quantity = item.quantity,
                total_amount = item.total_amount,
                total_items = item.total_items,
                total_invoice = item.total_amount + item.freight_value,
                total_incentive = item.total_items - item.total_amount,
                freight_value = item.freight_value,
                // shipment_date = not known yet
                // delivery_date = not known yet
                order_status = OrderStatus.INVOICED,
                unit_price = item.unit_price,
            };

             this.orderEntries.State[id].Add(orderEntry);
        }

        await this.orderEntries.WriteStateAsync();
    }

    public async Task ProcessPaymentConfirmed(PaymentConfirmed paymentConfirmed)
    {
        string id = new StringBuilder(paymentConfirmed.customer.CustomerId).Append('-').Append(paymentConfirmed.orderId).ToString();
        foreach (var item in this.orderEntries.State[id])
        {
            item.order_status = OrderStatus.PAYMENT_PROCESSED;
        }
        await this.orderEntries.WriteStateAsync();
    }

    public async Task ProcessPaymentFailed(PaymentFailed paymentFailed)
    {
        string id = new StringBuilder(paymentFailed.customer.CustomerId).Append('-').Append(paymentFailed.orderId).ToString();
        foreach (var item in this.orderEntries.State[id])
        {
            item.order_status = OrderStatus.PAYMENT_FAILED;
        }
        await this.orderEntries.WriteStateAsync();
    }

    public async Task ProcessShipmentNotification(ShipmentNotification shipmentNotification)
    {
        string id = new StringBuilder(shipmentNotification.customerId).Append('-').Append(shipmentNotification.orderId).ToString();
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
                item.delivery_status = PackageStatus.delivered;
                item.delivery_date = shipmentNotification.eventDate;
                // TODO log delivered entries and remove them from state
            }
                
        }
        await this.orderEntries.WriteStateAsync();
    }

    public async Task ProcessDeliveryNotification(DeliveryNotification deliveryNotification)
    {
        string id = new StringBuilder(deliveryNotification.customerId).Append('-').Append(deliveryNotification.orderId).ToString();
        var entry = this.orderEntries.State[id].Where(oe=>oe.product_id == deliveryNotification.productId).First();
        entry.package_id = deliveryNotification.packageId;
        entry.delivery_date = deliveryNotification.deliveryDate;
        await this.orderEntries.WriteStateAsync();
    }

    public Task<SellerDashboard> QueryDashboard()
    {
        // Queries not present in Orleans: https://github.com/dotnet/orleans/issues/4232
        var entries = this.orderEntries.State.SelectMany(x=>x.Value).ToList();
        OrderSellerView view = new OrderSellerView()
        {
            seller_id = this.sellerId,
            count_orders = this.orderEntries.State.Count(),
            count_items = entries.Count(),
            total_amount = entries.Sum(x=>x.total_amount),
            total_freight = entries.Sum(x=>x.freight_value),
            total_incentive = entries.Sum(x=>x.total_incentive),
            total_items = entries.Sum(x=>x.total_items),
            
        };
        return Task.FromResult(new SellerDashboard(view,entries));
    }
}


