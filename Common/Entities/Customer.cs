namespace Common.Entities
{

    public class Customer
    {
        // olist data set
        public int id { get; set; }

        // added
        public string first_name { get; set; } = "";

        public string last_name { get; set; } = "";

        public string address { get; set; } = "";

        public string complement { get; set; } = "";

        public string birth_date { get; set; } = "";

        // olist data set
        public string zip_code { get; set; } = "";

        public string city { get; set; } = "";

        public string state { get; set; } = "";

        // card
        public string card_number { get; set; } = "";

        public string card_security_number { get; set; } = "";

        public string card_expiration { get; set; } = "";

        public string card_holder_name { get; set; } = "";

        public string card_type { get; set; } = "";

        // statistics
        public int success_payment_count { get; set; } = 0;

        public int failed_payment_count { get; set; } = 0;

        public int delivery_count { get; set; } = 0;

        public int next_order_id { get; set; }

        // additional
        public string data { get; set; } = "";

        public Customer(){ }

    }
}

