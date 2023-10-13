using Common.Entities;
using Common.Requests;
using Orleans.Concurrency;

namespace OrleansApp.Interfaces
{

    public interface ICartActor : IGrainWithIntegerKey
    {
        public Task AddItem(CartItem item);

        public Task NotifyCheckout(CustomerCheckout basketCheckout);

        [ReadOnly]
        public Task<Cart> GetCart();

        public Task Seal();
    }
}