using System;
namespace Common.Entities
{
    /**
     * 
     */
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

        // public int pending_deliveries_count { get; set; }

        public int delivery_count { get; set; } = 0;

        // public int abandoned_cart_count { get; set; }

        // public float total_spent_items { get; set; }

        // public float total_spent_freights { get; set; }

        // additional
        public string data { get; set; } = "";

    }
}

