using Common.Entities;
using Common.Requests;

namespace Orleans.Interfaces
{
    public interface IProductActor : IGrainWithIntegerCompoundKey
    {
        Task SetProduct(Product product);

        Task<Product> GetProduct();

        Task DeleteProduct(DeleteProduct deleteProduct);

        Task UpdatePrice(UpdatePrice updatePrice);

    }
}
