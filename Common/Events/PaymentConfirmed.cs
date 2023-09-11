using Common.Entities;
using Common.Requests;

namespace Common.Events
{
    public class PaymentConfirmed
    { 
        public CustomerCheckout customer { get; set; }
        public int orderId { get; set; }
        public float totalAmount { get; set; }
        public List<OrderItem> items { get; set; }
        public DateTime date { get; set; }
        public int instanceId { get; set; }

        public PaymentConfirmed(CustomerCheckout customer, int orderId, float totalAmount, List<OrderItem> items, DateTime date, int instanceId)
        {
            this.customer = customer;
            this.orderId = orderId;
            this.totalAmount = totalAmount;
            this.items = items;
            this.date = date;
            this.instanceId = instanceId;
        }
    }
}

