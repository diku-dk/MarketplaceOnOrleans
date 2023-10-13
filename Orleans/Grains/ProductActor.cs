using Common;
using Common.Entities;
using Common.Events;
using Common.Requests;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;
using OrleansApp.Infra;
using OrleansApp.Interfaces;
using Orleans.Runtime;

namespace OrleansApp.Grains;

[Reentrant]
public sealed class ProductActor : Grain, IProductActor
{
    private readonly AppConfig config;
    private readonly IPersistentState<Product> product;
    private readonly ILogger<ProductActor> _logger;

    public ProductActor([PersistentState(
        stateName: "product",
        storageName: Constants.OrleansStorage)] IPersistentState<Product> state,
        AppConfig options,
        ILogger<ProductActor> _logger)
    {
        this.product = state;
        this.config = options;
        this._logger = _logger;
    }

    public override Task OnActivateAsync(CancellationToken token)
    {
        // int primaryKey = (int) this.GetPrimaryKeyLong(out string keyExtension);
        return Task.CompletedTask;
    }

    public async Task SetProduct(Product product)
    {
        this.product.State = product;
        this.product.State.active = true;
        this.product.State.created_at = DateTime.UtcNow;
        if(this.config.OrleansStorage)
            await this.product.WriteStateAsync();
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
    }

    public async Task Reset()
    {
        this.product.State.updated_at = DateTime.UtcNow;
        this.product.State.version = "0";
        if(this.config.OrleansStorage)
            await this.product.WriteStateAsync();
    }

}

