namespace Common.Entities;

public class CartItem {

    public int SellerId { get; set; }

    public int ProductId { get; set; }

    public string ProductName { get; set; }

    public float UnitPrice { get; set; }

    public float FreightValue { get; set; }

    public int Quantity { get; set; }

    public float Voucher { get; set; } = 0;

    public string Version { get; set; }

    public CartItem() { }

    public override string ToString()
    {
        return "{ SellerId "+SellerId+" ProductId "+ProductId+" }";
    }
}
