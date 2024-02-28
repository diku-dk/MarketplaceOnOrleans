using Common.Config;
using Common.Entities;
using Common.Events;
using Common.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans.Abstract;
using Orleans.Interfaces.SellerView;
using Orleans.Runtime;
using OrleansApp.Infra;
using SellerMS.Infra;
using System.Text;
using System.Text.Json;
using Orleans.Concurrency;

namespace Orleans.Grains.SellerView;

/**
 * Actor resposible for maintaining the correcteness of the seller view dashboard
 * TODO improvement: Initialize a new DbContext for each request to avoid the "connection is busy"
 * exception on concurrent async calls. A DbContextFactory may help. Related links:
 * https://learn.microsoft.com/en-us/ef/ef6/fundamentals/working-with-dbcontext#lifetime
 * https://github.com/npgsql/efcore.pg/issues/1901#issuecomment-1016282126
 */
[Reentrant]
public sealed class SellerViewActor : AbstractSellerActor, ISellerViewActor
{
    private readonly SellerDbContext dbContext;

    private readonly Dictionary<(int customerId, int orderId), List<int>> cache;

    private OrderSellerView EMPTY_SELLER_VIEW;

    private bool cachedViewIsDirty = false;

    private SellerDashboard sellerDashboardCached;

    public SellerViewActor(
        SellerDbContext dbContext,
        [PersistentState("seller", Constants.OrleansStorage)] IPersistentState<Seller> seller,
        IAuditLogger persistence,
        AppConfig options, 
        ILogger<SellerViewActor> logger) 
        : base(seller, persistence, options, logger)
    {
        this.dbContext = dbContext;
        this.cache = new();
        this.sellerDashboardCached = sellerDashboardCached = new SellerDashboard(
                EMPTY_SELLER_VIEW,
                new List<OrderEntry>()
            );
    }

    public override async Task OnActivateAsync(CancellationToken token)
    {
        await base.OnActivateAsync(token);

        // create or refresh materialized view
        this.dbContext.Database.ExecuteSqlRaw(SellerDbContext.CreateCustomOrderSellerViewSql(this.sellerId));
        this.dbContext.Database.ExecuteSqlRaw(SellerDbContext.GetRefreshCustomOrderSellerViewSql(this.sellerId));

        this.EMPTY_SELLER_VIEW = new(this.sellerId);
    }

    protected override Task ProcessNewOrderEntries(InvoiceIssued invoiceIssued, List<OrderEntry> orderEntries)
    {
        // if duplicate, discard event to avoid computing wrong view
        var ID = (invoiceIssued.customer.CustomerId, invoiceIssued.orderId);
        if(this.cache.ContainsKey(ID)) {
            this.logger.LogWarning("Seller {0} - Customer ID {1} Order ID {2} already exists.", this.sellerId, invoiceIssued.customer.CustomerId, invoiceIssued.orderId);
            return Task.CompletedTask; 
        }

        using (var txCtx = this.dbContext.Database.BeginTransaction())
        {
            this.dbContext.OrderEntries.AddRange(orderEntries);
            this.dbContext.SaveChanges();
            txCtx.Commit();
        }

        cache.Add(ID, orderEntries.Select(o => o.id).ToList());

        // cleaning tracking for new entries in this context
        this.dbContext.ChangeTracker.Clear();

        // refresh its own view
        this.dbContext.Database.ExecuteSqlRaw(SellerDbContext.GetRefreshCustomOrderSellerViewSql(this.sellerId));

        // mark cached view as dirty to force retrieval from DB
        this.cachedViewIsDirty = true;

        return Task.CompletedTask;
    }

    public override Task ProcessPaymentConfirmed(PaymentConfirmed paymentConfirmed)
    {
        /*
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
        */
        return Task.CompletedTask;
    }

    public override Task ProcessPaymentFailed(PaymentFailed paymentFailed)
    {
        /*
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
        */
        return Task.CompletedTask;
    }

    public override Task ProcessShipmentNotification(ShipmentNotification shipmentNotification)
    {
        // although it saves some remote calls, do not lead to significant improvement
        if (shipmentNotification.status != ShipmentStatus.concluded){
            return Task.CompletedTask;
        }

        using (var txCtx = this.dbContext.Database.BeginTransaction())
        {
            var ID = (shipmentNotification.customerId, shipmentNotification.orderId);
            if(!this.cache.ContainsKey(ID)){
                this.logger.LogWarning("Seller {0} - Order ID {1} have not arrived yet! Skipping event...", this.sellerId, ID);
                return Task.CompletedTask; 
            }
            var ids = this.cache[ID];
            var orderEntries = this.dbContext.OrderEntries.Where(oe => ids.Contains(oe.id));

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
                    // no need to update status, the entry must be deleted from actor state
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
                    var LOG_ID = new StringBuilder(shipmentNotification.customerId).Append('-').Append(shipmentNotification.orderId).ToString();
                    this.persistence.Log(Name, LOG_ID, str);
                }

                this.cache.Remove(ID);

                // force removal of entries from the view
                this.dbContext.Database.ExecuteSqlRaw(SellerDbContext.GetRefreshCustomOrderSellerViewSql(this.sellerId));

                this.cachedViewIsDirty = true;
            }

        }
        // clean entity tracking
        this.dbContext.ChangeTracker.Clear();
        return Task.CompletedTask;
    }

    public override Task ProcessDeliveryNotification(DeliveryNotification deliveryNotification)
    {
        /*
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
        */
        return Task.CompletedTask;
    }

    public override Task<SellerDashboard> QueryDashboard()
    {
        if(!cachedViewIsDirty) return Task.FromResult(sellerDashboardCached);

        // this should have transaction isolation
        using (var txCtx = this.dbContext.Database.BeginTransaction())
        {
            sellerDashboardCached = new SellerDashboard(
                this.dbContext.OrderSellerView.FromSqlRaw($"SELECT * FROM public.order_seller_view_{this.sellerId}").AsEnumerable().FirstOrDefault(this.EMPTY_SELLER_VIEW),
                this.dbContext.OrderEntries.Where(oe => oe.seller_id == sellerId).ToList()
            );
        }
        // mark cached view as not dirty anymore
        this.cachedViewIsDirty = false;

        return Task.FromResult(sellerDashboardCached);
    }

}

