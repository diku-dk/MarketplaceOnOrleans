namespace Common.Entities
{
    /**
     * Product is based on info found in:
     * (i) https://dev.olist.com/docs/creating-a-product
     * (ii) Olist data set, order_items file
     * It is worthy to note that the attributes gtin, stock, package mesasures, photo, and tags are not considered
     * Besides, only one category is chosen as found in olist public data set
     */
    public class Product
	{
        public int seller_id { get; set; }

        public int product_id { get; set; }

        public string name { get; set; } = "";

        public string sku { get; set; } = "";

        public string category { get; set; } = "";

        public string description { get; set; } = "";

        public float price { get; set; }

        public float freight_value { get; set; }

        // https://dev.olist.com/docs/products
        // approved by default
        public string status { get; set; } = "";

        public bool active { get; set; }

        public Product() { }

    }
}

