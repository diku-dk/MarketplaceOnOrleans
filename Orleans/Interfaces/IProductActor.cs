using Common.Entities;
using Common.Requests;

namespace Orleans.Interfaces
{
    public interface IProductActor : IGrainWithIntegerCompoundKey
    {
        public Task SetProduct(Product product);

        public Task<Product> GetProduct();

        public Task DeleteProduct(DeleteProduct deleteProduct);

        public Task UpdatePrice(UpdatePrice updatePrice);

    }
}
