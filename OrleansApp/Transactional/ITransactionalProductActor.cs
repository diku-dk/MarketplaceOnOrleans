using Common.Entities;
using Common.Requests;
using Orleans.Concurrency;
using OrleansApp.Interfaces;

namespace OrleansApp.Transactional;

public interface ITransactionalProductActor : IProductActor
{
    [ReadOnly]
    [Transaction(TransactionOption.Create)]
    new Task<Product> GetProduct();

    [Transaction(TransactionOption.Create)]
    new Task SetProduct(Product product);

    [Transaction(TransactionOption.Create)]
    new Task ProcessProductUpdate(Product product);

    [Transaction(TransactionOption.Create)]
    new Task ProcessPriceUpdate(PriceUpdate priceUpdate);

    [Transaction(TransactionOption.Create)]
    new Task Reset();

}

