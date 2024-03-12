using System.Text;

namespace Common.Entities
{

    public sealed class Cart
    {
        // no inter identified within an actor. so it requires an id
        public int customerId;

        public CartStatus status { get; set; } = CartStatus.OPEN;

        public List<CartItem> items { get; set; } = new List<CartItem>();

        public Cart() {}

        public Cart(int customerId) {
            this.customerId = customerId;
        }

        public override string ToString()
        {
            return new StringBuilder().Append("customerId : ").Append(customerId).Append("status").Append(status.ToString()).ToString();
        }

    }
}

