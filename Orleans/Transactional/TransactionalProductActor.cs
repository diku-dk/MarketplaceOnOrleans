using Common.Entities;
using Common.Events;
using Common.Requests;
using Microsoft.Extensions.Logging;
using OrleansApp.Infra;
using Orleans.Transactions.Abstractions;
using Orleans.Streams;
using Common.Config;
using Orleans.Concurrency;

namespace OrleansApp.Transactional;

[Reentrant]
public sealed class TransactionalProductActor : Grain, ITransactionalProductActor
{
    private IStreamProvider streamProvider;
    private IAsyncStream<Product> stream;

    private readonly ITransactionalState<Product> product;
    private readonly AppConfig config;
    private readonly ILogger<TransactionalProductActor> logger;

    public TransactionalProductActor([TransactionalState(
        stateName: "product",
        storageName: Constants.OrleansStorage)] ITransactionalState<Product> state,
        AppConfig config,
        ILogger<TransactionalProductActor> _logger)
    {
        this.product = state;
        this.config = config;
        this.logger = _logger;
    }

    public override Task OnActivateAsync(CancellationToken token)
    {
        if (this.config.StreamReplication)
        {
            int primaryKey = (int)this.GetPrimaryKeyLong(out string keyExtension);
            string ID = string.Format("{0}|{1}", primaryKey, keyExtension);
            this.logger.LogInformation("Setting up replication in transactional product actor " + ID);
            this.streamProvider = this.GetStreamProvider(Constants.DefaultStreamProvider);
            this.stream = streamProvider.GetStream<Product>(Constants.ProductNameSpace, ID);
        }
        return Task.CompletedTask;
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

    public async Task ProcessPriceUpdate(PriceUpdate priceUpdate)
    {
        await this.product.PerformUpdate(p => { 
            p.price = priceUpdate.price; 
            p.updated_at = DateTime.UtcNow;
        });
        if (this.config.StreamReplication)
        {
            await this.stream.OnNextAsync(await this.product.PerformRead(p => p));
        }
    }

    public async Task ProcessProductUpdate(Product product)
    {
        ProductUpdated productUpdated = new ProductUpdated(product.seller_id, product.product_id, product.version);
        var stockGrain = this.GrainFactory.GetGrain<ITransactionalStockActor>(product.seller_id, product.product_id.ToString());
        Task task1 = this.product.PerformUpdate(p => {
            p.price = product.price;
            p.sku = product.sku;
            p.version = product.version;
            p.description = product.description;
            p.seller_id = product.seller_id;
            p.product_id = product.product_id;
            p.category = product.category;
            p.description = product.description;
            p.active = true;
            p.created_at = p.created_at;
            p.updated_at = DateTime.UtcNow;
            p.freight_value = product.freight_value;
            p.name = product.name;
            p.status = product.status;
        });
        
        Task task2 = stockGrain.ProcessProductUpdate(productUpdated);
        if (this.config.StreamReplication)
        {
            // transactional guarantee
            await Task.WhenAll(task1, task2);
            // wait for transaction success to replicate
            await this.stream.OnNextAsync(await this.product.PerformRead(p => p));
        }
        else
        {
            await Task.WhenAll(task1, task2);
        }
    }

    public Task Reset()
    {
        return this.product.PerformUpdate(p => {
            p.updated_at = DateTime.UtcNow;
            p.version = "0";
        });
    }

}

