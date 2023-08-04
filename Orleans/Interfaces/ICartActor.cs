using Common.Entities;
using Common.Requests;

namespace Orleans.Interfaces
{

    public interface ICartActor : IGrainWithIntegerKey
    {

        public Task AddItem(CartItem item);

        public Task NotifyCheckout(CustomerCheckout basketCheckout);

        public Task<Cart> GetCart();

        public Task Seal();

    }
}
