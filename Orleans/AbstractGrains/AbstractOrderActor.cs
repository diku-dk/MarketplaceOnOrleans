using System.Text.Json;
using Common;
using Common.Entities;
using Common.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Grains;
using Orleans.Infra;
using Orleans.Interfaces;
using Orleans.TransactionalGrains;

namespace Orleans.AbstractGrains;

public abstract class AbstractOrderActor : Grain, IOrderActor
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

    protected int customerId;
    protected readonly ILogger<OrderActor> _logger;
    protected bool UseTransactions;

    public AbstractOrderActor(IOptions<AppConfig> config, ILogger<OrderActor> _logger)
	{
        this._logger = _logger;
        this.UseTransactions = config.Value.UseTransactions;
	}

    public abstract Task<int> GetNextOrderId();

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

        int orderId = await GetNextOrderId();

        var invoiceNumber = Helper.GetInvoiceNumber(customerId, now, orderId);
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

        await UpdateOrderState(orderId, new(){
            order = order,
            orderItems = items,
            orderHistory = new(){new OrderHistory()
            {
                order_id = orderId,
                created_at = now,
                status = OrderStatus.INVOICED
            }
            } } );

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

        var sellerIds = items.Select(x => x.seller_id).ToHashSet();
        var tasks = new List<Task>(sellerIds.Count);
        foreach (var sellerID in sellerIds)
        {
            // https://stackoverflow.com/questions/59327843/multiple-implementations-of-same-grain-interface-using-orleans
            var sellerActor = GetSellerActor(sellerID);
            tasks.Add(sellerActor.ProcessNewInvoice(invoice));
        }
        await Task.WhenAll(tasks);

        var paymentActor = GrainFactory.GetGrain<IPaymentActor>(this.customerId, "");
        await paymentActor.ProcessPayment(invoice);
    }

    public virtual ISellerActor GetSellerActor(int sellerId)
    {
        return GrainFactory.GetGrain<ISellerActor>(sellerId, "Orleans.Grains.SellerActor");
    }

    public async Task ProcessShipmentNotification(ShipmentNotification shipmentNotification)
    {
        DateTime now = DateTime.UtcNow;

        OrderStatus orderStatus = OrderStatus.READY_FOR_SHIPMENT;
        if (shipmentNotification.status == ShipmentStatus.delivery_in_progress) orderStatus = OrderStatus.IN_TRANSIT;
        if (shipmentNotification.status == ShipmentStatus.concluded) orderStatus = OrderStatus.DELIVERED;

        OrderState orderState = UseTransactions ? await GetOrderFromStateAsync(shipmentNotification.orderId) : GetOrderFromState(shipmentNotification.orderId);
        if (orderState is null){
            _logger.LogWarning("Possible interleaving for customer ID {0} shipment customer ID {1} shipment order ID {2} status {3}", this.customerId, shipmentNotification.customerId, shipmentNotification.orderId, shipmentNotification.status);
            return;
        }

        orderState.orderHistory.Add( new()
        {
            order_id = shipmentNotification.orderId,
            created_at = now,
            status = orderStatus
        } );
        orderState.order.status = orderStatus;
        orderState.order.updated_at = now;

        if (orderState.order.status == OrderStatus.DELIVERED)
        {
            orderState.order.delivered_customer_date = shipmentNotification.eventDate;
        
            // log finished order
            var str = JsonSerializer.Serialize(orderState);
            Helper.OrderLog.Put(orderState.order.customer_id.ToString() + "-" + shipmentNotification.orderId.ToString(), str);

            await RemoveOrderFromState(shipmentNotification.orderId);
        }
        
    }

    public virtual Task<OrderState> GetOrderFromStateAsync(int orderId)
    {
        throw new ApplicationException("GetOrderFromStateAsync not implemented");
    }
    public virtual OrderState GetOrderFromState(int orderId)
    {
        throw new ApplicationException("GetOrderFromState not implemented");
    }

    public abstract Task RemoveOrderFromState(int orderId);

    public abstract Task UpdateOrderState(int orderId, OrderState value);

    public abstract Task<List<Order>> GetOrders();

    public abstract Task<int> GetNumOrders();

    public virtual Task TestTransaction(Order order)
    {
        throw new NotImplementedException();
    }
}

