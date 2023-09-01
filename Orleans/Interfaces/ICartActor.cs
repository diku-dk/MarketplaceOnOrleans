using Common.Entities;
using Common.Requests;

namespace Orleans.Interfaces
{

    public interface ICartActor : IGrainWithIntegerKey
    {
        public Task AddItem(CartItem item);

        public Task<bool> NotifyCheckout(CustomerCheckout basketCheckout);

        public Task<Cart> GetCart();

        public Task Seal();
    }
}