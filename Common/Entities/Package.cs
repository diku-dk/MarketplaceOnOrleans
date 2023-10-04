namespace Common.Entities
{
	public class Package
	{
        public int shipment_id;

		// PK
		public int order_id;
        public int customer_id;
		public int package_id;

        // FK
        // product identification
        public int seller_id;
        public int product_id;

        public string product_name = "";

        public float freight_value;

		// date the shipment has actually been performed
		public DateTime shipping_date;

        // delivery date
        public DateTime delivery_date;

		public int quantity;
        
        public PackageStatus status { get; set; }

        public Package() { }
    }
}

