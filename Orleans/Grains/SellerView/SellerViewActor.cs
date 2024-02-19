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
using Orleans.Concurrency;

namespace Orleans.Grains.SellerView;

[Reentrant]
public class SellerViewActor : AbstractSellerActor, ISellerViewActor
{
    private readonly SellerDbContext dbContext;

    public SellerViewActor(
        SellerDbContext dbContext,
        [PersistentState("seller", Constants.OrleansStorage)] IPersistentState<Seller> seller,
        //IAuditLogger persistence,
        AppConfig options,
        ILogger<SellerActor> logger)
        //: base(seller, persistence, options, logger)
        : base(seller, options, logger)
    {
        this.dbContext = dbContext;
    }

    protected override async Task ProcessNewOrderEntries(InvoiceIssued invoiceIssued, List<OrderEntry> orderEntries)
    {
        foreach (var orderEntry in orderEntries)
        {
            var sql = @"
                INSERT INTO public.order_entries (
                    customer_id,
                    order_id,
                    product_id,
                    seller_id,
                    product_name,
                    product_category,
                    unit_price,
                    quantity,
                    total_items,
                    total_amount,
                    total_incentive,
                    total_invoice,
                    freight_value,
                    order_status,
                    delivery_status
                ) VALUES (
                    {0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14}
                )
                ON CONFLICT (customer_id, order_id, product_id, seller_id) DO NOTHING
            ";

            var parameters = new object[]
            {
                orderEntry.customer_id,
                orderEntry.order_id,
                orderEntry.product_id,
                orderEntry.seller_id,
                orderEntry.product_name,
                orderEntry.product_category,
                orderEntry.unit_price,
                orderEntry.quantity,
                orderEntry.total_items,
                orderEntry.total_amount,
                orderEntry.total_incentive,
                orderEntry.total_invoice,
                orderEntry.freight_value,
                orderEntry.order_status.ToString(),
                orderEntry.delivery_status.ToString()
            };

            await dbContext.Database.ExecuteSqlRawAsync(sql, parameters); // Commit the transaction automatically
        }

        dbContext.ChangeTracker.Clear();
        await dbContext.Database.ExecuteSqlRawAsync(SellerDbContext.RefreshMaterializedView);
    }

    public override async Task ProcessPaymentConfirmed(PaymentConfirmed paymentConfirmed)
    {
        var orderEntries = this.dbContext.OrderEntries.Where(oe => oe.customer_id == paymentConfirmed.customer.CustomerId && oe.order_id == paymentConfirmed.orderId);
        foreach (var item in orderEntries)
        {
            item.order_status = OrderStatus.PAYMENT_PROCESSED;
            this.dbContext.Entry(item).State = EntityState.Modified;
        }
        await this.dbContext.SaveChangesAsync();
        // clean entity tracking
        this.dbContext.ChangeTracker.Clear();
    }

    public override async Task ProcessPaymentFailed(PaymentFailed paymentFailed)
    {
        var orderEntries = this.dbContext.OrderEntries.Where(oe => oe.customer_id == paymentFailed.customer.CustomerId && oe.order_id == paymentFailed.orderId);
        foreach (var item in orderEntries)
        {
            item.order_status = OrderStatus.PAYMENT_FAILED;
            this.dbContext.Entry(item).State = EntityState.Modified;
        }
        await this.dbContext.SaveChangesAsync();
        // clean entity tracking
        this.dbContext.ChangeTracker.Clear();
    }

    public override async Task ProcessShipmentNotification(ShipmentNotification shipmentNotification)
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

        await this.dbContext.SaveChangesAsync();

        if (shipmentNotification.status == ShipmentStatus.concluded)
        {
            // log delivered entries and remove them from view
            if (this.config.LogRecords)
            {
                var str = JsonSerializer.Serialize(orderEntries.ToList());
                var ID = new StringBuilder(shipmentNotification.customerId).Append('-').Append(shipmentNotification.orderId).ToString();
                //this.persistence.Log(Name, ID, str);
            }
            // force removal of entries from the view
            this.dbContext.Database.ExecuteSqlRaw(SellerDbContext.RefreshMaterializedView);
        }

        // clean entity tracking
        this.dbContext.ChangeTracker.Clear();
    }

    public override async Task ProcessDeliveryNotification(DeliveryNotification deliveryNotification)
    {
        using (var txCtx = this.dbContext.Database.BeginTransaction())
        {
            var entry = this.dbContext.OrderEntries.Where(oe => oe.customer_id == deliveryNotification.customerId && oe.order_id == deliveryNotification.orderId && deliveryNotification.productId == oe.product_id).FirstOrDefault();
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

    private static readonly OrderSellerView EMPTY_SELLER_VIEW = new();

    public override Task<SellerDashboard> QueryDashboard()
    {
        SellerDashboard sellerDashboard;
        // this should be isolated
        sellerDashboard = new SellerDashboard(
            this.dbContext.OrderSellerView.Where(v => v.seller_id == sellerId).AsEnumerable().FirstOrDefault(EMPTY_SELLER_VIEW),
            this.dbContext.OrderEntries.Where(oe => oe.seller_id == sellerId && (oe.order_status == OrderStatus.INVOICED || oe.order_status == OrderStatus.READY_FOR_SHIPMENT || oe.order_status == OrderStatus.IN_TRANSIT || oe.order_status == OrderStatus.PAYMENT_PROCESSED)).ToList()
        );
        return Task.FromResult(sellerDashboard);
    }

}

