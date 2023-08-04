using System;
namespace Common.Entities
{
    /**
	 * Based on the payload found in:
	 * https://dev.olist.com/docs/orders-grouping-by-shipping_limite_date
	 * "id": "2a44f8af-dbed-4f47-9a48-0832e3306194",
     *  "created_at": "2021-10-08T16:11:57.171099Z",
     *   "status": "ready_to_ship"
     * Originally, olist maintains an order history list for each
     * item in an order. That makes sense because each item is 
     * individually shipped and tracked. In this benchmark, all items are shipped
     * together, and only later they are individually updated in shipment actor.
	 */
    public class OrderHistory
	{
		// PK. 
		public int id { get; set; }
        // FK can be ommitted if document-oriented model (as a nested object) is adopted
        public int order_id { get; set; }

        public DateTime created_at { get; set; }

        public OrderStatus status { get; set; }

    }
}

