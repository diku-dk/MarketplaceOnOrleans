using Common.Entities;
using Common.Events;
using Orleans.Concurrency;
using Orleans.Interfaces;
using Orleans.Runtime;
using System.Globalization;
using System.Text;

namespace Orleans.Grains
{
    [Reentrant]
    public class OrderActor : Grain, IOrderActor
    {

        private static readonly CultureInfo enUS = CultureInfo.CreateSpecificCulture("en-US");
        private static readonly DateTimeFormatInfo dtfi = enUS.DateTimeFormat;

        private readonly IPersistentState<Order> orderState;
        private readonly IPersistentState<List<OrderItem>> orderItemsState;

        // perhaps the customer actor can provide this info
        // private readonly IPersistentState<Dictionary<int, CustomerOrder>> customerOrderState;
        //private int customerId;

        private int nextOrderId;

        public OrderActor(
            [PersistentState(stateName: "order", storageName: "OrleansStorage")]
            IPersistentState<Order> orderState,
            [PersistentState(stateName: "orderItems", storageName: "OrleansStorage")]
            IPersistentState<List<OrderItem>> orderItemsState
            //[PersistentState(stateName: "customerOrder", storageName: "OrleansStorage")]
            //IPersistentState<Dictionary<int, CustomerOrder>> customerOrderState
            ) 
        { 
            this.orderState = orderState;
            this.orderItemsState = orderItemsState;
            //this.customerOrderState = customerOrderState;
            this.nextOrderId = 1;
        }

        public override async Task OnActivateAsync(CancellationToken token)
        {
            //this.customerId = this.GetPrimaryKeyint();
            await base.OnActivateAsync(token);
        }


        public async Task Checkout(ReserveStock reserveStock)
        {
            var now = DateTime.Now;

            // coordinate with all IStock
            List<Task<ItemStatus>> statusResp = new(reserveStock.items.Count());

            foreach (var item in reserveStock.items)
            {
                var stockActor = GrainFactory.GetGrain<IStockActor>(item.SellerId, item.ProductId.ToString(), null);
                statusResp.Add(stockActor.AttemptReservation(item.Quantity));
            }

            await Task.WhenAll(statusResp);
            int idx = 0;
            var itemsToCheckout = new List<CartItem>(reserveStock.items.Count());
            List<Task> stockTasks= new List<Task>();
            foreach (var item in reserveStock.items)
            {
                if (statusResp[idx].Result == ItemStatus.IN_STOCK)
                {
                    itemsToCheckout.Add(item);
                    var stockActor = GrainFactory.GetGrain<IStockActor>(item.SellerId, item.ProductId.ToString(), null);
                    stockTasks.Add(stockActor.ConfirmReservation(item.Quantity));
                }
            }

            await Task.WhenAll(stockTasks);

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
                                                             .Append("-").Append(customerOrder.next_order_id);


            Order newOrder = new()
            {
                id = nextOrderId,
                customer_id = reserveStock.customerCheckout.CustomerId,
                status = OrderStatus.INVOICED,
                created_at = DateTime.UtcNow,
                purchase_date = reserveStock.timestamp,
                total_amount = total_amount,
                total_items = total_items,
                total_freight = total_freight,
                total_incentive = total_incentive,
                total_invoice = total_amount + total_freight,
                count_items = itemsToCheckout.Count(),

            };

            nextOrderId++;

            return;
        }

        public class CustomerOrder
        {

            public int customer_id { get; set; }

            public int next_order_id { get; set; }

            public CustomerOrder() { }

        }

    }
}
