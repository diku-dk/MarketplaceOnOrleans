using Common.Entities;
using Common.Events;
using Common.Requests;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;
using OrleansApp.Infra;
using OrleansApp.Interfaces;
using Orleans.Runtime;
using Orleans.Streams;
using Common.Config;
using Common.Integration;
using Orleans.Infra.Redis;

namespace OrleansApp.Grains;

[Reentrant]
public sealed class ProductActor : Grain, IProductActor
{
    private IStreamProvider streamProvider;
    private IAsyncStream<Product> stream;
    
    private readonly AppConfig config;
    private readonly IPersistentState<Product> product;
    private readonly IRedisConnectionFactory redisFactory;
    private readonly ILogger<ProductActor> logger;

    public ProductActor([PersistentState(
        stateName: "product",
        storageName: Constants.OrleansStorage)] IPersistentState<Product> state,
        AppConfig options,
        ILogger<ProductActor> _logger,
        IRedisConnectionFactory factory = null)
    {
        this.product = state;
        this.config = options;
        this.logger = _logger;
        this.redisFactory = factory;
    }

    public override Task OnActivateAsync(CancellationToken token)
    { 
        if (this.config.StreamReplication)
        {
            int primaryKey = (int) this.GetPrimaryKeyLong(out string keyExtension);
            string ID = string.Format("{0}|{1}", primaryKey, keyExtension);
            this.logger.LogInformation("Setting up stream replication in product actor " + ID);
            this.streamProvider = this.GetStreamProvider(Constants.DefaultStreamProvider);
            this.stream = streamProvider.GetStream<Product>(Constants.ProductNameSpace, ID);
        }
        return Task.CompletedTask;
    }

    public async Task SetProduct(Product product)
    {
        this.product.State = product;
        this.product.State.active = true;
        this.product.State.created_at = DateTime.UtcNow;
        if(this.config.OrleansStorage)
            await this.product.WriteStateAsync();
        if (this.config.StreamReplication)
        {
            await this.stream.OnNextAsync(this.product.State);
        }
        else if (this.config.RedisReplication)
        {
            string key = product.seller_id + "-" + product.product_id;
            ProductReplica productReplica = new ProductReplica(key, product.version, product.price);
            await this.redisFactory.SaveProductAsync(key, productReplica);
        }
    }

    public async Task ProcessProductUpdate(Product product)
    {
        product.created_at = this.product.State.created_at;
        product.updated_at = DateTime.UtcNow;
        this.product.State = product;
        if(this.config.OrleansStorage)
            await this.product.WriteStateAsync();
      
        ProductUpdated productUpdated = new ProductUpdated(product.seller_id, product.product_id, product.version);
        var stockGrain = this.GrainFactory.GetGrain<IStockActor>(product.seller_id, product.product_id.ToString());
        await stockGrain.ProcessProductUpdate(productUpdated);

        if (this.config.StreamReplication)
        {
            await this.stream.OnNextAsync(this.product.State);
        } 
        else if (this.config.RedisReplication)
        {
            string key = product.seller_id + "-" + product.product_id;
            ProductReplica productReplica = new ProductReplica(key, product.version, product.price);
            await this.redisFactory.UpdateProductAsync(key, productReplica);
        }
    }

    public Task<Product> GetProduct()
    {
        return Task.FromResult(this.product.State);
    }

    public async Task ProcessPriceUpdate(PriceUpdate priceUpdate)
    {
        this.product.State.price = priceUpdate.price;
        this.product.State.updated_at = DateTime.UtcNow;
        if(this.config.OrleansStorage)
            await this.product.WriteStateAsync();
        if (this.config.StreamReplication)
        {
            await this.stream.OnNextAsync(this.product.State);
        }
        else if (this.config.RedisReplication)
        {
            string key = this.product.State.seller_id + "-" + this.product.State.product_id;
            ProductReplica productReplica = new ProductReplica(key, this.product.State.version, priceUpdate.price);
            await this.redisFactory.UpdateProductAsync(key, productReplica);
        }
    }

    public async Task Reset()
    {
        this.product.State.updated_at = DateTime.UtcNow;
        this.product.State.version = "0";
        if(this.config.OrleansStorage)
            await this.product.WriteStateAsync();
    }

}

