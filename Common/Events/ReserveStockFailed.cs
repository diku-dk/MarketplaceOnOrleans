using Common.Entities;
using Common.Requests;

namespace Common.Events;

public class ReserveStockFailed
{
    public DateTime timestamp {get; set; }
    public CustomerCheckout customerCheckout {get; set; }
    public List<ProductStatus> products {get; set; }
    public int instanceId {get; set; }

    public ReserveStockFailed(){ }

    public ReserveStockFailed(DateTime timestamp, CustomerCheckout customerCheckout, List<ProductStatus> products, int instanceId)
    {
        this.timestamp = timestamp;
        this.customerCheckout = customerCheckout;
        this.products = products;
        this.instanceId = instanceId;
    }
}

