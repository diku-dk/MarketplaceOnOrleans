using Common.Entities;
using Common.Events;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;
using Orleans.Infra;
using Orleans.Interfaces;

namespace Orleans.Grains;

[Reentrant]
public class OrderActor : Grain, IOrderActor
{
    ILogger<OrderActor> _logger;
    Dictionary<int, (Order, List<OrderItem>)> orders;   // <order ID, order state, order item state>

    private int customerId;
    private int nextOrderId;

    public OrderActor(ILogger<OrderActor> _logger) 
    {
        this._logger = _logger;
    }

    public override async Task OnActivateAsync(CancellationToken token)
    {
        nextOrderId = 1;
        customerId = (int)this.GetPrimaryKeyLong();
        orders = new Dictionary<int, (Order, List<OrderItem>)>();
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
            statusResp.Add(stockActor.AttemptReservation(item.Quantity));
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

            float sumVouchers = item.Vouchers.Sum();

            if (total_item - sumVouchers > 0)
            {
                total_amount -= sumVouchers;
                total_incentive += sumVouchers;
                total_item -= sumVouchers;
            }
            else
            {
                total_amount -= total_item;
                total_incentive += total_item;
                total_item = 0;
            }

            totalPerItem.Add(item.ProductId, total_item);
        }

        /*
        // TODO get from customer
        CustomerOrder customerOrder = null; // this.customerOrderState.State[reserveStock.customerCheckout.CustomerId];

        if (customerOrder is null)
        {
            customerOrder = new()
            {
                customer_id = reserveStock.customerCheckout.CustomerId,
                next_order_id = 1
            };
            // this.customerOrderState.State.Add(reserveStock.customerCheckout.CustomerId, customerOrder);
        }
        else
        {
            customerOrder.next_order_id += 1;
        }
        // await this.customerOrderState.WriteStateAsync();
        

        StringBuilder stringBuilder = new StringBuilder().Append(reserveStock.customerCheckout.CustomerId)
                                                         .Append("-").Append(now.ToString("d", enUS))
                                                         .Append("-").Append(nextOrderId);
        */

        var orderId = nextOrderId++;
        var order = new Order()
        {
            id = orderId,
            customer_id = reserveStock.customerCheckout.CustomerId,
            status = OrderStatus.INVOICED,
            created_at = DateTime.UtcNow,
            purchase_date = reserveStock.timestamp,
            total_amount = total_amount,
            total_items = total_items,
            total_freight = total_freight,
            total_incentive = total_incentive,
            total_invoice = total_amount + total_freight,
            count_items = itemsToCheckout.Count()
        };
        
        List<OrderItem> items = new();
        itemsToCheckout.ForEach(x =>
        {
            items.Add(new OrderItem
            {
                order_id = orderId,
                //order_item_id = ,             // ????
                product_id = x.ProductId,
                product_name = x.ProductName,
                seller_id = x.SellerId,
                unit_price = x.UnitPrice,
                //shipping_limit_date = ,      // ????
                freight_value = x.FreightValue,
                quantity = x.Quantity,
                total_items = total_items,
                total_amount = total_amount,
                vouchers = x.Vouchers,
            });
        });

        orders.Add(orderId, (order, items));

        var invoice = new InvoiceIssued
        (
            reserveStock.customerCheckout,
            orderId,
            Helper.GetInvoiceNumber(customerId, now, orderId),
            DateTime.UtcNow,
            total_amount,
            items,
            reserveStock.instanceId
        );
        var paymentActor = GrainFactory.GetGrain<IPaymentActor>(customerId);
        await paymentActor.ProcessPayment(invoice);
        _logger.LogWarning($"Notify payment actor InvoiceIssued. ");

        var tasks = new List<Task>();
        var sellers = items.Select(x => x.seller_id).ToHashSet();
        foreach (var sellerID in sellers)
        {
            var sellerActor = GrainFactory.GetGrain<ISellerActor>(sellerID);
            tasks.Add(sellerActor.ProcessNewInvoice(invoice));
        }
        await Task.WhenAll(tasks);
        _logger.LogWarning($"Notify {sellers.Count} sellers InvoiceIssued. ");
    }

    public class CustomerOrder
    {

        public int customer_id { get; set; }

        public int next_order_id { get; set; }

        public CustomerOrder() { }

    }

}
