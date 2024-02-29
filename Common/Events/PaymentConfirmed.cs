using Common.Entities;
using Common.Requests;
using Newtonsoft.Json;

namespace Common.Events;

public sealed class PaymentConfirmed
{
    [JsonProperty("customer")]
    public CustomerCheckout customer { get; set; }

    public int orderId { get; set; }
    public float totalAmount { get; set; }
    public List<OrderItem> items { get; set; }
    public DateTime date { get; set; }
    public string instanceId { get; set; }

    public PaymentConfirmed(CustomerCheckout customer, int orderId, float totalAmount, List<OrderItem> items, DateTime date, string instanceId)
    {
        this.customer = customer;
        this.orderId = orderId;
        this.totalAmount = totalAmount;
        this.items = items;
        this.date = date;
        this.instanceId = instanceId;
    }
}


