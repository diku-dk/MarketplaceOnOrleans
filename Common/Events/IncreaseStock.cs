namespace Common.Events;

public class IncreaseStock
{
    public int seller_id { get; set; }

    public int product_id { get; set; }

    public int quantity { get; set; }

    public IncreaseStock(){ }

    public IncreaseStock(int seller_id, int product_id, int quantity)
    {
        this.seller_id = seller_id;
        this.product_id = product_id;
        this.quantity = quantity;
    }
}

