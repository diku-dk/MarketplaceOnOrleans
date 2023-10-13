using Common.Entities;
using Common.Events;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using OrleansApp.Infra;
using OrleansApp.Interfaces;
using System.Text;
using System.Globalization;
using Common;
using Orleans.Concurrency;
using System.Diagnostics;

namespace OrleansApp.Abstract;

[Reentrant]
public abstract class AbstractOrderActor : Grain, IOrderActor
{
    private static string Name = typeof(AbstractOrderActor).FullName;

    public class NextOrderIdState
    {
        public int Value { get; set; }
        public NextOrderIdState() { this.Value = 0; }
        public NextOrderIdState(int value) { this.Value = value; }
        public NextOrderIdState GetNextOrderId()
        {
            this.Value++;
            return this;
        }
    }

    public class OrderState
    {
        public Order order { get; set; }
        public List<OrderItem> orderItems { get; set; }
        public List<OrderHistory> orderHistory { get; set; }
        public OrderState() { }
    }

    protected readonly AppConfig config;
    protected readonly ILogger<AbstractOrderActor> logger;
    protected readonly IPersistence persistence;

    protected int customerId;

    private static readonly CultureInfo enUS = CultureInfo.CreateSpecificCulture("en-US");
    private static readonly DateTimeFormatInfo dtfi = enUS.DateTimeFormat;

    static AbstractOrderActor()
    {
        // https://learn.microsoft.com/en-us/dotnet/api/system.globalization.datetimeformatinfo?view=net-7.0
        dtfi.ShortDatePattern = "yyyyMMdd";
    }

    private static string GetInvoiceNumber(int customerId, DateTime timestamp, int orderId)
        => new StringBuilder().Append(customerId).Append('-')
                              .Append(timestamp.ToString("d", enUS)).Append('-')
                              .Append(orderId).ToString();

    public AbstractOrderActor(IPersistence persistence,
                                AppConfig options,
                                ILogger<AbstractOrderActor> _logger)
    {
        this.persistence = persistence;
        this.config = options;
        this.logger = _logger;
    }

    public override Task OnActivateAsync(CancellationToken token)
    {
        this.customerId = (int)this.GetPrimaryKeyLong();
        return Task.CompletedTask;
    }
   

    public async Task Checkout(ReserveStock reserveStock)
    {
        // Debug.Assert(reserveStock.customerCheckout.CustomerId == this.customerId);

        var now = DateTime.Now;

        // coordinate with all IStock
        List<Task<ItemStatus>> statusResp = new(reserveStock.items.Count());

        for (var idx = 0; idx < reserveStock.items.Count; idx++)
        {
            var item = reserveStock.items[idx];
            var stockActor = GetStockActor(item.SellerId, item.ProductId);
            statusResp.Insert(idx, stockActor.AttemptReservation(item));
        }
        await Task.WhenAll(statusResp);

        // collect all items that are in stock
        var itemsToCheckout = new List<CartItem>();
        for (var idx = 0; idx < reserveStock.items.Count; idx++)
        {
            if (statusResp[idx].Result == ItemStatus.IN_STOCK)
            {
                itemsToCheckout.Add(reserveStock.items[idx]);
            }
        }

        // calculate total freight_value
        float total_freight = 0;
        foreach (var item in itemsToCheckout)
        {
            total_freight += item.FreightValue;
        }

        float total_amount = 0;
        foreach (var item in itemsToCheckout)
        {
            total_amount += (item.UnitPrice * item.Quantity);
        }

        float total_items = total_amount;

        Dictionary<(int, int), float> totalPerItem = new();
        float total_incentive = 0;
        foreach (var item in itemsToCheckout)
        {
            float total_item = item.UnitPrice * item.Quantity;

            if (total_item - item.Voucher > 0)
            {
                total_amount -= item.Voucher;
                total_incentive += item.Voucher;
                total_item -= item.Voucher;
            }
            else
            {
                total_amount -= total_item;
                total_incentive += total_item;
                total_item = 0;
            }

            totalPerItem.Add((item.SellerId, item.ProductId), total_item);
        }

        int orderId = await GetNextOrderId();
        var invoiceNumber = GetInvoiceNumber(this.customerId, now, orderId);
        var order = new Order()
        {
            id = orderId,
            customer_id = this.customerId,
            invoice_number = invoiceNumber,
            status = OrderStatus.INVOICED,
            purchase_date = reserveStock.timestamp,
            total_amount = total_amount,
            total_items = total_items,
            total_freight = total_freight,
            total_incentive = total_incentive,
            total_invoice = total_amount + total_freight,
            count_items = itemsToCheckout.Count,
            created_at = now,
            updated_at = now,
        };

        List<OrderItem> items = new();

        int id = 1;
        foreach (var item in itemsToCheckout)
        {
            items.Add(new OrderItem
            {
                order_id = orderId,
                order_item_id = id,
                product_id = item.ProductId,
                product_name = item.ProductName,
                seller_id = item.SellerId,
                unit_price = item.UnitPrice,
                quantity = item.Quantity,
                total_items = item.UnitPrice * item.Quantity,
                total_amount = totalPerItem[(item.SellerId, item.ProductId)],
                freight_value = item.FreightValue,
                shipping_limit_date = now.AddDays(3),
                voucher = item.Voucher
            });
            id++;
        }

        await InsertOrderIntoState(orderId, new()
        {
            order = order,
            orderItems = items,
            orderHistory = new(){new OrderHistory()
            {
                order_id = orderId,
                created_at = now,
                status = OrderStatus.INVOICED
            }
            }
        });

        var invoice = new InvoiceIssued
        (
            reserveStock.customerCheckout,
            orderId,
            invoiceNumber,
            now,
            order.total_invoice,
            items,
            reserveStock.instanceId
        );

        var tasks = new List<Task>();
        var sellerIds = items.Select(x => x.seller_id).Distinct();
        foreach (var sellerID in sellerIds)
        {
            var sellerActor = GetSellerActor(sellerID);
            var invoiceCustom = new InvoiceIssued
            (
                reserveStock.customerCheckout,
                orderId,
                invoiceNumber,
                now,
                order.total_invoice,
                items.Where(id => id.seller_id == sellerID).ToList(),
                reserveStock.instanceId
            );
            tasks.Add(sellerActor.ProcessNewInvoice(invoiceCustom));
        }
        await Task.WhenAll(tasks);

        var paymentActor = GetPaymentActor(this.customerId);
        await paymentActor.ProcessPayment(invoice);
    }

    public async Task ProcessPaymentConfirmed(PaymentConfirmed paymentConfirmed)
    {
        DateTime now = DateTime.UtcNow;
        OrderState orderState = config.OrleansTransactions ? await GetOrderFromStateAsync(paymentConfirmed.orderId) : GetOrderFromState(paymentConfirmed.orderId);
        if (orderState is null)
        {
            logger.LogWarning("Cannot process payment confirmed event because invoice has not been found");
            return;
        }
        orderState.orderHistory.Add(new()
        {
            order_id = paymentConfirmed.orderId,
            created_at = now,
            status = OrderStatus.PAYMENT_PROCESSED
        });
        var order = orderState.order;
        order.status = OrderStatus.PAYMENT_PROCESSED;
        order.updated_at = now;
        await UpdateOrderState(orderState.order.id, orderState);
    }

    public async Task ProcessPaymentFailed(PaymentFailed paymentFailed)
    {
        DateTime now = DateTime.UtcNow;
        OrderState orderState = config.OrleansTransactions ? await GetOrderFromStateAsync(paymentFailed.orderId) : GetOrderFromState(paymentFailed.orderId);
        if (orderState is null)
        {
            logger.LogWarning("Cannot process payment confirmed event because invoice has not been found");
            return;
        }
        orderState.orderHistory.Add(new()
        {
            order_id = paymentFailed.orderId,
            created_at = now,
            status = OrderStatus.PAYMENT_PROCESSED
        });
        var order = orderState.order;
        order.status = OrderStatus.PAYMENT_PROCESSED;
        order.updated_at = now;
        // log finished order
        if (config.LogRecords)
        {
            var str = JsonSerializer.Serialize(orderState);
            var sb = new StringBuilder(order.customer_id.ToString()).Append('-').Append(paymentFailed.orderId).ToString();
            await persistence.Log(Name, sb.ToString(), str);
        }
        await RemoveOrderFromState(paymentFailed.orderId);
    }

    public async Task ProcessShipmentNotification(ShipmentNotification shipmentNotification)
    {
        DateTime now = DateTime.UtcNow;

        OrderStatus orderStatus = OrderStatus.READY_FOR_SHIPMENT;
        if (shipmentNotification.status == ShipmentStatus.delivery_in_progress) orderStatus = OrderStatus.IN_TRANSIT;
        if (shipmentNotification.status == ShipmentStatus.concluded) orderStatus = OrderStatus.DELIVERED;

        OrderState orderState = config.OrleansTransactions ? await GetOrderFromStateAsync(shipmentNotification.orderId) : GetOrderFromState(shipmentNotification.orderId);
        if (orderState is null)
        {
            logger.LogWarning("Possible interleaving for customer ID {0} shipment customer ID {1} shipment order ID {2} status {3}", this.customerId, shipmentNotification.customerId, shipmentNotification.orderId, shipmentNotification.status);
            return;
        }

        orderState.orderHistory.Add(new()
        {
            order_id = shipmentNotification.orderId,
            created_at = now,
            status = orderStatus
        });
        orderState.order.status = orderStatus;
        orderState.order.updated_at = now;

        if (orderState.order.status == OrderStatus.DELIVERED)
        {
            orderState.order.delivered_customer_date = shipmentNotification.eventDate;

            // log finished order
            if (config.LogRecords)
            {
                var str = JsonSerializer.Serialize(orderState);
                var sb = new StringBuilder(orderState.order.customer_id.ToString()).Append('-').Append(shipmentNotification.orderId).ToString();
                await persistence.Log(Name, sb.ToString(), str);
            }
            await RemoveOrderFromState(shipmentNotification.orderId);
        }
    }

    public abstract ISellerActor GetSellerActor(int sellerId);

    public abstract IStockActor GetStockActor(int sellerId, int productId);

    public abstract IPaymentActor GetPaymentActor(int customerId);

    protected abstract Task<int> GetNextOrderId();

    public abstract Task<List<Order>> GetOrders();

    public abstract Task<int> GetNumOrders();

    public abstract Task Reset();

    public virtual Task<OrderState> GetOrderFromStateAsync(int orderId)
    {
        throw new ApplicationException("GetOrderFromStateAsync not implemented");
    }

    public virtual OrderState GetOrderFromState(int orderId)
    {
        throw new ApplicationException("GetOrderFromState not implemented");
    }

    public abstract Task RemoveOrderFromState(int orderId);

    public abstract Task InsertOrderIntoState(int orderId, OrderState value);

    public abstract Task UpdateOrderState(int orderId, OrderState value);

}
