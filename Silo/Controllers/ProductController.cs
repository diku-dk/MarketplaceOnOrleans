using System.Net;
using Common.Config;
using Common.Entities;
using Common.Requests;
using Microsoft.AspNetCore.Mvc;
using OrleansApp.Interfaces;
using OrleansApp.Transactional;

namespace Silo.Controllers;

[ApiController]
public sealed class ProductController : ControllerBase
{
    private readonly ILogger<ProductController> logger;
    private readonly bool OrleansTransactions;
    private readonly ITransactionClient transactionClient;

    public ProductController(AppConfig config, IHost host, ILogger<ProductController> logger)
    {
        this.logger = logger;
        this.OrleansTransactions = config.OrleansTransactions;
        this.transactionClient = config.OrleansTransactions ? host.Services.GetRequiredService<ITransactionClient>() : null;
    }

    [HttpPost]
    [Route("/product")]
    public async Task<ActionResult> AddProduct([FromServices] IGrainFactory grainFactory,
        [FromBody] Product product)
    {
        this.logger.LogDebug("[AddProduct] for ID {0}|{1}", product.seller_id, product.product_id);
        if(this.OrleansTransactions){
            var grain = this.GetTxProductActor(grainFactory, product.seller_id, product.product_id);
            await this.transactionClient.RunTransaction(
                TransactionOption.Create, 
                async () =>
                {
                    await grain.SetProduct(product);
                });
        }
        else
        {
            var grain = this.GetDefaultProductActor(grainFactory, product.seller_id, product.product_id);
            await grain.SetProduct(product);
        }
        return Ok();
    }

    [HttpGet("/product/{sellerId:long}/{productId:long}")]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(Product), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<Product>> GetBySellerIdAndProductId([FromServices] IGrainFactory grainFactory, int sellerId, int productId)
    {
        Product product = null;
        if(this.OrleansTransactions){
            var grain = this.GetTxProductActor(grainFactory, sellerId, productId);
            await this.transactionClient.RunTransaction(
                TransactionOption.Create,
                async () =>
                {
                    product = await grain.GetProduct();
                });
        }
        else
        {
            var grain = this.GetDefaultProductActor(grainFactory, sellerId, productId);
            product = await grain.GetProduct();
        }
        if (product is null)
            return NotFound();
        return Ok(product);
    }

    [HttpPatch]
    [Route("/product")]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult> ProcessPriceUpdate([FromServices] IGrainFactory grainFactory, [FromBody] PriceUpdate update)
    {
        this.logger.LogDebug("[ProcessPriceUpdate] for ID {0}|{1}", update.sellerId, update.productId);
        if(this.OrleansTransactions){
            var grain = this.GetTxProductActor(grainFactory, update.sellerId, update.productId);
            await this.transactionClient.RunTransaction(
                TransactionOption.Create, 
                async () =>
                {
                    await grain.ProcessPriceUpdate(update);
                });
        }
        else
        {
            var grain = this.GetDefaultProductActor(grainFactory, update.sellerId, update.productId);
            await grain.ProcessPriceUpdate(update);
        }
        return Accepted();
    }

    [HttpPut]
    [Route("/product")]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult> ProcessUpdateProduct([FromServices] IGrainFactory grainFactory, [FromBody] Product product)
    {
        this.logger.LogDebug("[ProcessUpdateProduct] for ID {0}|{1}", product.seller_id, product.product_id);
        if(this.OrleansTransactions){
            var grain = this.GetTxProductActor(grainFactory, product.seller_id, product.product_id);
            await this.transactionClient.RunTransaction(
                TransactionOption.Create, 
                async () =>
                {
                    await grain.ProcessProductUpdate(product);
                });
        }
        else
        {
            var grain = this.GetDefaultProductActor(grainFactory, product.seller_id, product.product_id);
            await grain.ProcessProductUpdate(product);
        }
        return Accepted();
    }

    private IProductActor GetDefaultProductActor(IGrainFactory grainFactory, int sellerId, int productId)
    {
        return grainFactory.GetGrain<IProductActor>(sellerId, productId.ToString());
    }

    private ITransactionalProductActor GetTxProductActor(IGrainFactory grainFactory, int sellerId, int productId)
    {
        return grainFactory.GetGrain<ITransactionalProductActor>(sellerId, productId.ToString());
    }

}

