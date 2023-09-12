namespace Common.Entities
{
	public class OrderItem
	{
        public int order_id { get; set; }

        public int order_item_id { get; set; }

        public int product_id { get; set; }

        public string product_name { get; set; } = "";

        public int seller_id { get; set; }

        // prices change over time
        public float unit_price { get; set; }

        public DateTime shipping_limit_date { get; set; }

        public float freight_value { get; set; }

        // not present in olist
        public int quantity { get; set; }

        // without freight value
        public float total_items { get; set; }

        // without freight value
        public float total_amount { get; set; }

        //
        public float voucher { get; set; }

    }
}

