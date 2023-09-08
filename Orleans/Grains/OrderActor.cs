using Common.Entities;
using Common.Events;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Orleans.Concurrency;
using Orleans.Infra;
using Orleans.Interfaces;
using Orleans.Runtime;
using RocksDbSharp;

namespace Orleans.Grains;

[Reentrant]
public class OrderActor : Grain, IOrderActor
{
    private readonly ILogger<OrderActor> _logger;
    // Dictionary<int, (Order, List<OrderItem>)> orders;   // <order ID, order state, order item state>

    private readonly IPersistentState<Dictionary<int,(Order, List<OrderItem>, List<OrderHistory>)>> orders;
    private readonly IPersistentState<int> nextOrderId;

    private int customerId;

    private RocksDb db;

    public OrderActor(
        [PersistentState(stateName: "orders", storageName: Constants.OrleansStorage)] IPersistentState<Dictionary<int,(Order, List<OrderItem>, List<OrderHistory>)>> orders,
        [PersistentState(stateName: "nextOrderId", storageName: Constants.OrleansStorage)] IPersistentState<int> nextOrderId,
        ILogger<OrderActor> _logger) 
    {
        this._logger = _logger;
        this.orders = orders;
        this.nextOrderId = nextOrderId;
    }

    public override async Task OnActivateAsync(CancellationToken token)
    {
        // persistence
        if(this.orders.State is null) this.orders.State = new();
        if(this.nextOrderId.State == 0) this.nextOrderId.State = 1;
        this.db = RocksDb.Open(Constants.rocksDBOption, typeof(OrderActor).FullName);

        this.customerId = (int)this.GetPrimaryKeyLong();
        await base.OnActivateAsync(token);
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
            if (statusResp[idx].Result == ItemStatus.IN_STOCK) 
                itemsToCheckout.Add(reserveStock.items[idx]);
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

        var orderId = nextOrderId.State++;
        var invoiceNumber = Helper.GetInvoiceNumber(customerId, now, orderId);
        var order = new Order()
        {
            id = orderId,
            customer_id = reserveStock.customerCheckout.CustomerId,
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
        foreach (var item in reserveStock.items)
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
                shipping_limit_date = now.AddDays(3)
            });
            id++;
        }

        orders.State.Add(orderId, (order, items, new(){new OrderHistory()
            {
                order_id = orderId,
                created_at = now,
                status = OrderStatus.INVOICED
            } } ));

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
        var sellers = items.Select(x => x.seller_id).ToHashSet();
        foreach (var sellerID in sellers)
        {
            var sellerActor = GrainFactory.GetGrain<ISellerActor>(sellerID);
            tasks.Add(sellerActor.ProcessNewInvoice(invoice));
        }
        await Task.WhenAll(tasks);
        _logger.LogWarning($"Notify {sellers.Count} sellers InvoiceIssued. ");

        var paymentActor = GrainFactory.GetGrain<IPaymentActor>(customerId);
        await paymentActor.ProcessPayment(invoice);
        _logger.LogWarning($"Notify payment actor InvoiceIssued. ");
    }

    public async Task ProcessShipmentNotification(ShipmentNotification shipmentNotification)
    {
        DateTime now = DateTime.UtcNow;

        OrderStatus orderStatus = OrderStatus.READY_FOR_SHIPMENT;
        if (shipmentNotification.status == ShipmentStatus.delivery_in_progress) orderStatus = OrderStatus.IN_TRANSIT;
        if (shipmentNotification.status == ShipmentStatus.concluded) orderStatus = OrderStatus.DELIVERED;

        orders.State[shipmentNotification.orderId].Item3.Add( new()
        {
            order_id = shipmentNotification.orderId,
            created_at = now,
            status = orderStatus
        } );
        var order = orders.State[shipmentNotification.orderId].Item1;
        order.status = orderStatus;
        order.updated_at = now;

        if (order.status == OrderStatus.DELIVERED)
        {
            order.delivered_customer_date = shipmentNotification.eventDate;
        
            // log finished order
            var str = JsonSerializer.Serialize(orders.State[shipmentNotification.orderId]);
            db.Put(order.customer_id.ToString() + "-" + shipmentNotification.orderId.ToString(), str);
            _logger.LogWarning($"Log shipment info to RocksDB. ");

            orders.State.Remove(shipmentNotification.orderId);
        }

        await orders.WriteStateAsync();
    }

    public Task<List<Order>> GetOrders()
    {
        var res = this.orders.State.Select(x=>x.Value.Item1).ToList();
        return Task.FromResult(res);
    }
}
