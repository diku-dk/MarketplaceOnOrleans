using Common;
using Common.Entities;
using Common.Events;
using Common.Requests;
using Microsoft.Extensions.Logging;
using Orleans.Infra;
using Orleans.Transactions.Abstractions;

namespace Orleans.Transactional;

public class TransactionalProductActor : Grain, ITransactionalProductActor
{
    private readonly ITransactionalState<Product> product;
    private readonly AppConfig config;
    private readonly ILogger<TransactionalProductActor> _logger;

    public TransactionalProductActor([TransactionalState(
        stateName: "product",
        storageName: Constants.OrleansStorage)] ITransactionalState<Product> state,
        AppConfig options,
        ILogger<TransactionalProductActor> _logger)
    {
        this.product = state;
        this.config = options;
        this._logger = _logger;
    }

    public Task SetProduct(Product product)
    {
        return this.product.PerformUpdate(p => {
            p.price = product.price;
            p.sku = product.sku;
            p.version = product.version;
            p.description = product.description;
            p.seller_id = product.seller_id;
            p.product_id = product.product_id;
            p.category = product.category;
            p.description = product.description;
            p.active = true;
            p.created_at = DateTime.UtcNow;
            p.freight_value = product.freight_value;
            p.name = product.name;
            p.status = product.status;
        });
    }

    public Task<Product> GetProduct()
    {
        return this.product.PerformRead(p => p);
    }

    public Task ProcessPriceUpdate(PriceUpdate priceUpdate)
    {
        return this.product.PerformUpdate(p => { 
            p.price = priceUpdate.price; 
            p.updated_at = DateTime.UtcNow;
        });
    }

    public Task ProcessProductUpdate(Product product)
    {
        ProductUpdated productUpdated = new ProductUpdated(product.seller_id, product.product_id, product.version);
        var stockGrain = this.GrainFactory.GetGrain<ITransactionalStockActor>(product.seller_id, product.product_id.ToString());
        Task task1 = this.product.PerformUpdate(product_ => {
            product.created_at = product_.created_at;
            product.updated_at = DateTime.UtcNow;
            product_ = product;
        });
        
        Task task2 = stockGrain.ProcessProductUpdate(productUpdated);
        return Task.WhenAll(task1, task2);
    }

    public Task Reset()
    {
        return this.product.PerformUpdate(p => {
            p.updated_at = DateTime.UtcNow;
            p.version = "0";
        });
    }


}

