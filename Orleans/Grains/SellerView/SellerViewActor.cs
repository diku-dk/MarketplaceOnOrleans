using Common.Config;
using Common.Entities;
using Common.Events;
using Common.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans.Abstract;
using Orleans.Interfaces.SellerView;
using Orleans.Runtime;
using OrleansApp.Grains;
using OrleansApp.Infra;
using SellerMS.Infra;
using System.Text;
using System.Text.Json;

namespace Orleans.Grains.SellerView;

public class SellerViewActor : AbstractSellerActor, ISellerViewActor
{
    /*
     *  
     *  a. finish implementation logic here OK
     *  b. generate migration  PK
        c. adapt seller controller to pick right seller actor (via delegate) OK
        d. the same in order, payment, shipment OK
        e. trigger view changes OK
        e. test query in postgresql. execute project
     */

    private readonly SellerDbContext dbContext;

    public SellerViewActor(
        SellerDbContext dbContext,
        [PersistentState("seller", Constants.OrleansStorage)] IPersistentState<Seller> seller,
        IAuditLogger persistence,
        AppConfig options, 
        ILogger<SellerActor> logger) 
        : base(seller, persistence, options, logger)
    {
        this.dbContext = dbContext;
    }

    protected override async Task ProcessNewOrderEntries(InvoiceIssued invoiceIssued, List<OrderEntry> orderEntries)
    {
        using (var txCtx = this.dbContext.Database.BeginTransaction())
        {
            this.dbContext.OrderEntries.AddRange(orderEntries);
            await txCtx.CommitAsync();
        }
        this.dbContext.Database.ExecuteSqlRaw($"REFRESH MATERIALIZED VIEW CONCURRENTLY public.{nameof(OrderSellerView)};");
    }

    public override async Task ProcessPaymentConfirmed(PaymentConfirmed paymentConfirmed)
    {
        using (var txCtx = this.dbContext.Database.BeginTransaction())
        {
            var orderEntries = this.dbContext.OrderEntries.Where(oe => oe.customer_id == paymentConfirmed.customer.CustomerId && oe.order_id == paymentConfirmed.orderId);
            foreach (var item in orderEntries)
            {
                item.order_status = OrderStatus.PAYMENT_PROCESSED;
            }
            this.dbContext.OrderEntries.UpdateRange(orderEntries);
            await txCtx.CommitAsync();
        }
    }

    public override async Task ProcessPaymentFailed(PaymentFailed paymentFailed)
    {
        using (var txCtx = this.dbContext.Database.BeginTransaction())
        {
            var orderEntries = this.dbContext.OrderEntries.Where(oe => oe.customer_id == paymentFailed.customer.CustomerId && oe.order_id == paymentFailed.orderId);
            foreach (var item in orderEntries)
            {
                item.order_status = OrderStatus.PAYMENT_FAILED;
            }
            this.dbContext.OrderEntries.UpdateRange(orderEntries);
            await this.dbContext.SaveChangesAsync();
            await txCtx.CommitAsync();
        }
    }

    public override async Task ProcessShipmentNotification(ShipmentNotification shipmentNotification)
    {
        using (var txCtx = this.dbContext.Database.BeginTransaction())
        {
            var orderEntries = this.dbContext.OrderEntries.Where(oe => oe.customer_id == shipmentNotification.customerId && oe.order_id == shipmentNotification.orderId);

            // log delivered entries and remove them from state
            if (shipmentNotification.status == ShipmentStatus.concluded)
            {
                if (this.config.LogRecords)
                {
                    var str = JsonSerializer.Serialize(orderEntries);
                    var ID = new StringBuilder(shipmentNotification.customerId).Append('-').Append(shipmentNotification.orderId).ToString();
                    await persistence.Log(Name, ID, str);
                }
                this.dbContext.OrderEntries.RemoveRange(orderEntries);
            }
            else
            {
                foreach (var item in orderEntries)
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
                    if (shipmentNotification.status == ShipmentStatus.concluded)
                    {
                        item.order_status = OrderStatus.DELIVERED;
                    }
                    */

                }
                this.dbContext.OrderEntries.UpdateRange(orderEntries);
            }
            await this.dbContext.SaveChangesAsync();
            await txCtx.CommitAsync();

            // force removal of entries from the view
            if (shipmentNotification.status == ShipmentStatus.concluded)
            {
                this.dbContext.Database.ExecuteSqlRaw($"REFRESH MATERIALIZED VIEW CONCURRENTLY public.{nameof(OrderSellerView)};");
            }

        }
    }

    public override async Task ProcessDeliveryNotification(DeliveryNotification deliveryNotification)
    {
        using (var txCtx = this.dbContext.Database.BeginTransaction())
        {
            var entry = this.dbContext.OrderEntries.Where(oe => oe.customer_id == deliveryNotification.customerId && oe.order_id == deliveryNotification.orderId && deliveryNotification.productId == oe.product_id).First();
            if (entry is not null)
            {
                entry.package_id = deliveryNotification.packageId;
                entry.delivery_status = PackageStatus.delivered;
                entry.delivery_date = deliveryNotification.deliveryDate;

            }
            await this.dbContext.SaveChangesAsync();
            await txCtx.CommitAsync();
        }
    }

    public override Task<SellerDashboard> QueryDashboard()
    {
        SellerDashboard sellerDashboard;
        using (var txCtx = dbContext.Database.BeginTransaction())
        {
            sellerDashboard = new SellerDashboard(
                this.dbContext.OrderSellerView.Where(v => v.seller_id == sellerId).FirstOrDefault(new OrderSellerView()),
                this.dbContext.OrderEntries.Where(oe => oe.seller_id == sellerId && (oe.order_status == OrderStatus.INVOICED || oe.order_status == OrderStatus.READY_FOR_SHIPMENT ||  oe.order_status == OrderStatus.IN_TRANSIT || oe.order_status == OrderStatus.PAYMENT_PROCESSED)).ToList()
            );
        }
        return Task.FromResult(sellerDashboard);
    }

}

