namespace Common.Entities
{
    public class ProductStatus
    {
        public int Id { get; set; }
        public ItemStatus Status { get; set; }
        public float UnitPrice { get; set; } = 0;
        public float OldUnitPrice { get; set; } = 0;
        public int QtyAvailable { get; set; } = 0;

        public ProductStatus() { }

        public ProductStatus(int id, ItemStatus status, float price, float oldPrice)
        {
            this.Id = id;
            this.Status = status;
            this.UnitPrice = price;
            this.OldUnitPrice = oldPrice;
        }

        public ProductStatus(int id, ItemStatus status)
        {
            this.Id = id;
            this.Status = status;
        }

        public ProductStatus(int id, ItemStatus status, int qtyAvailable)
        {
            this.Id = id;
            this.Status = status;
            this.QtyAvailable = qtyAvailable;
        }

    }
}

