using Common.Entities;
using Common.Events;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Orleans.Infra;
using Orleans.Interfaces;
using Orleans.Runtime;
using System.Text;
using System.Globalization;

namespace Orleans.Grains;

public class OrderActor : Grain, IOrderActor
{

    public class NextOrderState
    {
        public int Value { get; set; }
        public NextOrderState(){ this.Value = 0; }
        public NextOrderState(int value){ this.Value = value; }
        public NextOrderState GetNextOrderId()
        {
            return new NextOrderState(this.Value + 1);
        }
    }

    public class OrderState
    {
        public Order order { get; set; }
        public List<OrderItem> orderItems { get; set; }
        public List<OrderHistory> orderHistory { get; set; }
        public OrderState(){ }
    }

    private readonly ILogger<OrderActor> _logger;
    private readonly IPersistence _persistence;
   
    // Dictionary<int, (Order, List<OrderItem>)> orders;   // <order ID, order state, order item state>

    private readonly IPersistentState<Dictionary<int, OrderState>> orders;
    private readonly IPersistentState<NextOrderState> nextOrderId;

    private int customerId;

    private static readonly CultureInfo enUS = CultureInfo.CreateSpecificCulture("en-US");
    private static readonly DateTimeFormatInfo dtfi = enUS.DateTimeFormat;

    static OrderActor()
    {
        // https://learn.microsoft.com/en-us/dotnet/api/system.globalization.datetimeformatinfo?view=net-7.0
        dtfi.ShortDatePattern = "yyyyMMdd";
    }

    private static string GetInvoiceNumber(int customerId, DateTime timestamp, int orderId)
        => new StringBuilder().Append(customerId).Append('-')
                              .Append(timestamp.ToString("d", enUS)).Append('-')
                              .Append(orderId).ToString();

    public OrderActor(
        [PersistentState(stateName: "orders", storageName: Constants.OrleansStorage)] IPersistentState<Dictionary<int,OrderState>> orders,
        [PersistentState(stateName: "nextOrderId", storageName: Constants.OrleansStorage)] IPersistentState<NextOrderState> nextOrderId,
        ILogger<OrderActor> _logger,
        IPersistence _persistence) 
    {
        this._logger = _logger;
        this.orders = orders;
        this.nextOrderId = nextOrderId;
        this._persistence = _persistence;
    }

    public override Task OnActivateAsync(CancellationToken token)
    {
        // persistence
        if(this.orders.State is null) this.orders.State = new();
        if(this.nextOrderId.State is null) this.nextOrderId.State = new();
        
        this.customerId = (int)this.GetPrimaryKeyLong();
        return Task.CompletedTask;
    }

    public async Task Checkout(ReserveStock reserveStock)
    {
        var now = DateTime.Now;

        // coordinate with all IStock
        List<Task<ItemStatus>> statusResp = new(reserveStock.items.Count());

        foreach (var item in reserveStock.items)
        {
            var stockActor = GrainFactory.GetGrain<IStockActor>(item.SellerId, item.ProductId.ToString());
            statusResp.Add(stockActor.AttemptReservation(item));
        }
        await Task.WhenAll(statusResp);

        // collect all items that are in stock
        var itemsToCheckout = new List<CartItem>();
        for (var idx = 0; idx < reserveStock.items.Count; idx++)
        {
            if (statusResp[idx].Result == ItemStatus.IN_STOCK) {
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

        Dictionary<int, float> totalPerItem = new();
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

            totalPerItem.Add(item.ProductId, total_item);
        }

        nextOrderId.State = nextOrderId.State.GetNextOrderId();
        int orderId = nextOrderId.State.Value;
        var invoiceNumber = GetInvoiceNumber(customerId, now, orderId);
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
            count_items = itemsToCheckout.Count(),
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
                total_amount = totalPerItem[item.ProductId],
                freight_value = item.FreightValue,
                shipping_limit_date = now.AddDays(3),
                voucher = item.Voucher
            });
            id++;
        }

        orders.State.Add(orderId, new(){
            order = order,
            orderItems = items,
            orderHistory = new(){new OrderHistory()
            {
                order_id = orderId,
                created_at = now,
                status = OrderStatus.INVOICED
            }
            } } );

        var tasks = new List<Task>
        {
            nextOrderId.WriteStateAsync(),
            orders.WriteStateAsync()
        };
        await Task.WhenAll(tasks);

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

        tasks.Clear();
        var sellerIds = items.Select(x => x.seller_id).ToHashSet();
        foreach (var sellerID in sellerIds)
        {
            var sellerActor = GrainFactory.GetGrain<ISellerActor>(sellerID);
            tasks.Add(sellerActor.ProcessNewInvoice(invoice));
        }
        await Task.WhenAll(tasks);

        var paymentActor = GrainFactory.GetGrain<IPaymentActor>(this.customerId);
        await paymentActor.ProcessPayment(invoice);
    }

    public async Task ProcessShipmentNotification(ShipmentNotification shipmentNotification)
    {
        DateTime now = DateTime.UtcNow;

        OrderStatus orderStatus = OrderStatus.READY_FOR_SHIPMENT;
        if (shipmentNotification.status == ShipmentStatus.delivery_in_progress) orderStatus = OrderStatus.IN_TRANSIT;
        if (shipmentNotification.status == ShipmentStatus.concluded) orderStatus = OrderStatus.DELIVERED;

        if( !orders.State.ContainsKey(shipmentNotification.orderId)){
            _logger.LogWarning("Possible interleaving for customer ID {0} shipment customer ID {1} shipment order ID {2} status {3}", this.customerId, shipmentNotification.customerId, shipmentNotification.orderId, shipmentNotification.status);
            return;
        }

        orders.State[shipmentNotification.orderId].orderHistory.Add( new()
        {
            order_id = shipmentNotification.orderId,
            created_at = now,
            status = orderStatus
        } );
        var order = orders.State[shipmentNotification.orderId].order;
        order.status = orderStatus;
        order.updated_at = now;

        if (order.status == OrderStatus.DELIVERED)
        {
            order.delivered_customer_date = shipmentNotification.eventDate;
        
            // log finished order
            var str = JsonSerializer.Serialize(orders.State[shipmentNotification.orderId]);
            var sb = new StringBuilder(order.customer_id.ToString()).Append('-').Append(shipmentNotification.orderId).ToString();
            _persistence.Log(typeof(OrderActor).FullName, sb.ToString(), str);

            orders.State.Remove(shipmentNotification.orderId);
        }

        await orders.WriteStateAsync();
    }

    public Task<List<Order>> GetOrders()
    {
        var res = this.orders.State.Select(x=>x.Value.order).ToList();
        return Task.FromResult(res);
    }

    public Task<int> GetNumOrders()
    {
        return Task.FromResult(this.nextOrderId.State.Value);
    }

}
