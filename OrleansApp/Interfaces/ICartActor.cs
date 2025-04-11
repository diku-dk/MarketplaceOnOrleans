using Common.Entities;
using Common.Requests;
using Orleans.Concurrency;

namespace OrleansApp.Interfaces;

public interface ICartActor : IGrainWithIntegerKey
{
    public Task AddItem(CartItem item);

    [ReadOnly]
    public Task<List<CartItem>> GetItems();

    public Task NotifyCheckout(CustomerCheckout basketCheckout);

    [ReadOnly]
    public Task<Cart> GetCart();

    public Task Seal();

    [ReadOnly]
    public Task<List<CartItem>> GetHistory(string tid);
}
