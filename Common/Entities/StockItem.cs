namespace Common.Entities
{
	public class StockItem
	{
        public int seller_id { get; set; }

        public int product_id { get; set; }

        public int qty_available { get; set; }

        public int qty_reserved { get; set; }

        public int order_count { get; set; }

        public int ytd { get; set; }

        public string data { get; set; }

    }
}