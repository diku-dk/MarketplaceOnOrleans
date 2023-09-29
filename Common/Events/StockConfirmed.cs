using Common.Entities;
using Common.Requests;
using Newtonsoft.Json;

namespace Common.Events;

public class StockConfirmed
{
    public DateTime timestamp { get; set; }

    //[JsonProperty("customer")]
    public CustomerCheckout customerCheckout { get; set; }

    public List<CartItem> items { get; set; }

    public string instanceId { get; set; }

    public StockConfirmed(){ }

    public StockConfirmed(DateTime timestamp, CustomerCheckout customerCheckout, List<CartItem> items, string instanceId)
    {
        this.timestamp = timestamp;
        this.customerCheckout = customerCheckout;
        this.items = items;
        this.instanceId = instanceId;
    }
}


