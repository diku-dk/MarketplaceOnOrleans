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

    protected override Task ProcessNewOrderEntries(InvoiceIssued invoiceIssued, List<OrderEntry> orderEntries)
    {
        using (var txCtx = this.dbContext.Database.BeginTransaction())
        {
            this.dbContext.OrderEntries.AddRange(orderEntries);
            this.dbContext.SaveChanges();
            txCtx.Commit();
        }
        // cleaning tracking for new entries in this context
        this.dbContext.ChangeTracker.Clear();
        this.dbContext.Database.ExecuteSqlRaw(SellerDbContext.RefreshMaterializedView);
        return Task.CompletedTask;
    }

    public override Task ProcessPaymentConfirmed(PaymentConfirmed paymentConfirmed)
    {
        using (var txCtx = this.dbContext.Database.BeginTransaction())
        {
            var orderEntries = this.dbContext.OrderEntries.Where(oe => oe.customer_id == paymentConfirmed.customer.CustomerId && oe.order_id == paymentConfirmed.orderId);
            foreach (var item in orderEntries)
            {
                item.order_status = OrderStatus.PAYMENT_PROCESSED;
                this.dbContext.Entry(item).State = EntityState.Modified;
            }
            this.dbContext.SaveChanges();
            txCtx.Commit();
        }
        // clean entity tracking
        this.dbContext.ChangeTracker.Clear();
        return Task.CompletedTask;
    }

    public override Task ProcessPaymentFailed(PaymentFailed paymentFailed)
    {
        using (var txCtx = this.dbContext.Database.BeginTransaction())
        {
            var orderEntries = this.dbContext.OrderEntries.Where(oe => oe.customer_id == paymentFailed.customer.CustomerId && oe.order_id == paymentFailed.orderId);
            foreach (var item in orderEntries)
            {
                item.order_status = OrderStatus.PAYMENT_FAILED;
                this.dbContext.Entry(item).State = EntityState.Modified;
            }
            this.dbContext.SaveChanges();
            txCtx.Commit();
        }
        // clean entity tracking
        this.dbContext.ChangeTracker.Clear();
        return Task.CompletedTask;
    }

    public override Task ProcessShipmentNotification(ShipmentNotification shipmentNotification)
    {
        using (var txCtx = this.dbContext.Database.BeginTransaction())
        {
            var orderEntries = this.dbContext.OrderEntries.Where(oe => oe.customer_id == shipmentNotification.customerId && oe.order_id == shipmentNotification.orderId);
            foreach (var item in orderEntries)
            {
                if (shipmentNotification.status == ShipmentStatus.approved)
                {
                    item.order_status = OrderStatus.READY_FOR_SHIPMENT;
                    item.shipment_date = shipmentNotification.eventDate;
                    item.delivery_status = PackageStatus.ready_to_ship;
                    this.dbContext.Entry(item).State = EntityState.Modified;
                }
                if (shipmentNotification.status == ShipmentStatus.delivery_in_progress)
                {
                    item.order_status = OrderStatus.IN_TRANSIT;
                    item.delivery_status = PackageStatus.shipped;
                    this.dbContext.Entry(item).State = EntityState.Modified;
                }
                if (shipmentNotification.status == ShipmentStatus.concluded)
                {
                    // item.order_status = OrderStatus.DELIVERED;
                    this.dbContext.Entry(item).State = EntityState.Deleted;
                }
            }

            this.dbContext.SaveChanges();
            txCtx.Commit();

            if (shipmentNotification.status == ShipmentStatus.concluded)
            {
                // log delivered entries and remove them from view
                if(this.config.LogRecords){
                    var str = JsonSerializer.Serialize(orderEntries.ToList());
                    var ID = new StringBuilder(shipmentNotification.customerId).Append('-').Append(shipmentNotification.orderId).ToString();
                    this.persistence.Log(Name, ID, str);
                }
                // force removal of entries from the view
                this.dbContext.Database.ExecuteSqlRaw(SellerDbContext.RefreshMaterializedView);
            }

        }
        // clean entity tracking
        this.dbContext.ChangeTracker.Clear();
        return Task.CompletedTask;
    }

    public override Task ProcessDeliveryNotification(DeliveryNotification deliveryNotification)
    {
        using (var txCtx = this.dbContext.Database.BeginTransaction())
        {
            var entry = this.dbContext.OrderEntries.Where(oe => oe.customer_id == deliveryNotification.customerId && oe.order_id == deliveryNotification.orderId && deliveryNotification.productId == oe.product_id).First();
            if (entry is not null)
            {
                entry.package_id = deliveryNotification.packageId;
                entry.delivery_status = PackageStatus.delivered;
                entry.delivery_date = deliveryNotification.deliveryDate;

                this.dbContext.Entry(entry).State = EntityState.Modified;

                this.dbContext.SaveChanges();
                txCtx.Commit();
            }
        }
        this.dbContext.ChangeTracker.Clear();
        return Task.CompletedTask;
    }

    private static readonly OrderSellerView EMPTY_SELLER_VIEW = new();

    public override Task<SellerDashboard> QueryDashboard()
    {
        SellerDashboard sellerDashboard;
        // this should be isolated
        using (var txCtx = this.dbContext.Database.BeginTransaction())
        {
            sellerDashboard = new SellerDashboard(
                this.dbContext.OrderSellerView.Where(v => v.seller_id == sellerId).AsEnumerable().FirstOrDefault(EMPTY_SELLER_VIEW),
                this.dbContext.OrderEntries.Where(oe => oe.seller_id == sellerId && (oe.order_status == OrderStatus.INVOICED || oe.order_status == OrderStatus.READY_FOR_SHIPMENT ||  oe.order_status == OrderStatus.IN_TRANSIT || oe.order_status == OrderStatus.PAYMENT_PROCESSED)).ToList()
            );
        }
        return Task.FromResult(sellerDashboard);
    }

}

