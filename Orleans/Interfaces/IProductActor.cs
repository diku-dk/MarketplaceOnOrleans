using Common.Entities;
using Common.Requests;

namespace Orleans.Interfaces
{
    public interface IProductActor : IGrainWithIntegerCompoundKey
    {
        Task SetProduct(Product product);

        Task<Product> GetProduct();

        Task ProcessProductUpdate(Product product);

        Task ProcessPriceUpdate(PriceUpdate priceUpdate);

    }
}
