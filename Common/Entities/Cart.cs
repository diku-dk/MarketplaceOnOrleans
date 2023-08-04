using System.Text;

namespace Common.Entities
{

    public class Cart
    {
        // no inter identified within an actor. so it requires an id
        public int customerId { get; set; } = 0;

        public CartStatus status { get; set; } = CartStatus.OPEN;

        public IList<CartItem> items { get; set; } = new List<CartItem>();

        public int instanceId { get; set; }

        // to return
        public List<ProductStatus>? divergencies { get; set; }

        // for dapr
        public Cart() {
            this.status = CartStatus.OPEN;
        }

        public override string ToString()
        {
            return new StringBuilder().Append("customerId : ").Append(customerId).Append("status").Append(status.ToString()).ToString();
        }

    }
}

