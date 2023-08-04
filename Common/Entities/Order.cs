namespace Common.Entities
{
    /*
     * Order is based on two sources:
     * (i) Olist data set (kaggle)
     * (ii) Olist developer API: https://dev.olist.com/docs/retrieving-order-informations
     * The total attribute is also added to sum the value of all products in the order.
     */
    public class Order
	{
        // PK
        public int id { get; set; }

        // FK
        public int customer_id { get; set; }

        public OrderStatus status { get; set; }

        public DateTime purchase_date { get; set; }

        // public string approved_at { get; set; }

        // added
        public DateTime payment_date { get; set; }

        public DateTime delivered_carrier_date { get; set; }

        public DateTime delivered_customer_date { get; set; }

        public DateTime estimated_delivery_date { get; set; }

        // dev
        public int count_items { get; set; }

        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }

        public float total_amount { get; set; }
        public float total_freight { get; set; }
        public float total_incentive { get; set; }
        public float total_invoice { get; set; }
        public float total_items { get; set; }

        public Order()
        {
            this.status = OrderStatus.CREATED;
        }

    }
}

