using Common.Entities;
using Common.Requests;
using Orleans.Concurrency;

namespace OrleansApp.Interfaces;

public interface IProductActor : IGrainWithIntegerCompoundKey
{
    Task SetProduct(Product product);

    [ReadOnly]
    Task<Product> GetProduct();

    Task ProcessProductUpdate(Product product);

    Task ProcessPriceUpdate(PriceUpdate priceUpdate);

    Task Reset();
}

