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
        IAuditLogger persistence,
        AppConfig options,
        ILogger<SellerActor> logger)
        : base(seller, persistence, options, logger)        
    {
        this.dbContext = dbContext;
    }

    protected override async Task ProcessNewOrderEntries(InvoiceIssued invoiceIssued, List<OrderEntry> orderEntries)
    {
        var sql = new StringBuilder(@"
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
            ) VALUES ");

        foreach (var orderEntry in orderEntries)
        {
            sql.Append("(");
            sql.Append(orderEntry.customer_id).Append(",");
            sql.Append(orderEntry.order_id).Append(",");
            sql.Append(orderEntry.product_id).Append(",");
            sql.Append(orderEntry.seller_id).Append(",");
            sql.Append("'").Append(orderEntry.product_name).Append("',");
            sql.Append("'").Append(orderEntry.product_category).Append("',");
            sql.Append(orderEntry.unit_price).Append(",");
            sql.Append(orderEntry.quantity).Append(",");
            sql.Append(orderEntry.total_items).Append(",");
            sql.Append(orderEntry.total_amount).Append(",");
            sql.Append(orderEntry.total_incentive).Append(",");
            sql.Append(orderEntry.total_invoice).Append(",");
            sql.Append(orderEntry.freight_value).Append(",");
            sql.Append("'").Append(orderEntry.order_status.ToString()).Append("',");
            sql.Append("'").Append(orderEntry.delivery_status.ToString()).Append("'),");
        }
        sql.Remove(sql.Length - 1, 1);
        sql.Append(" ON CONFLICT (customer_id, order_id, product_id, seller_id) DO NOTHING");

        await dbContext.Database.ExecuteSqlRawAsync(sql.ToString());

        dbContext.ChangeTracker.Clear();
    }

    public override Task ProcessPaymentConfirmed(PaymentConfirmed paymentConfirmed)
    {
        return Task.CompletedTask;
    }
   
    public override Task ProcessPaymentFailed(PaymentFailed paymentFailed)
    {        
        return Task.CompletedTask;
    }

    public override async Task ProcessShipmentNotification(ShipmentNotification shipmentNotification)
    {
        var customerId = shipmentNotification.customerId;
        var orderId = shipmentNotification.orderId;
        var eventDate = shipmentNotification.eventDate.ToString("yyyy-MM-dd HH:mm:ss");
        string sql = string.Empty;
        List<OrderEntry> orderEntriesForLogging = null;

        if (shipmentNotification.status == ShipmentStatus.approved)
        {
            sql = $@"
            UPDATE public.order_entries        
            SET order_status = {(int)OrderStatus.READY_FOR_SHIPMENT}, 
                shipment_date = '{eventDate}', 
                delivery_status = {(int)PackageStatus.ready_to_ship}
            WHERE customer_id = {customerId} AND order_id = {orderId};
            ";
        }
        else if (shipmentNotification.status == ShipmentStatus.delivery_in_progress)
        {
            sql = $@"
            UPDATE public.order_entries
            SET order_status = {(int)OrderStatus.IN_TRANSIT}, 
                delivery_status = {(int)PackageStatus.shipped}
            WHERE customer_id = {customerId} AND order_id = {orderId};
            ";
        }
        else if (shipmentNotification.status == ShipmentStatus.concluded)
        {
            if (this.config.LogRecords)
            {
                var fetchSql = $@"SELECT * FROM public.order_entries WHERE customer_id = {customerId} AND order_id = {orderId};";
                orderEntriesForLogging = await dbContext.OrderEntries.FromSqlRaw(fetchSql).ToListAsync();
                var str = JsonSerializer.Serialize(orderEntriesForLogging);
                var ID = new StringBuilder(shipmentNotification.customerId).Append('-').Append(shipmentNotification.orderId).ToString();
                //this.persistence.Log(Name, ID, str);
            }
            sql = $@"
            DELETE FROM public.order_entries
            WHERE customer_id = {customerId} AND order_id = {orderId};
            ";                              
        }

        if (!string.IsNullOrEmpty(sql))
        {
            await dbContext.Database.ExecuteSqlRawAsync(sql);
        }
    }

    public override Task ProcessDeliveryNotification(DeliveryNotification deliveryNotification)
    {
        return Task.CompletedTask;
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

