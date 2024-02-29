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
    private readonly GetProductActorDelegate callback;

    public ProductController(AppConfig config, ILogger<ProductController> logger)
    {
        this.logger = logger;
        this.callback = config.OrleansTransactions ? GetTransactionalProductActor : GetProductActor;
    }

    [HttpPost]
    [Route("/product")]
    public async Task<ActionResult> AddProduct([FromServices] IGrainFactory grains, [FromBody] Product product)
    {
        this.logger.LogDebug("[AddProduct] received for id {0} {1}", product.seller_id, product.product_id);
        await this.callback(grains,product.seller_id, product.product_id).SetProduct(product);
        return Ok();
    }

    [HttpGet("/product/{sellerId:long}/{productId:long}")]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(Product), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<Product>> GetBySellerIdAndProductId([FromServices] IGrainFactory grains, int sellerId, int productId)
    {
        var product = await this.callback(grains, sellerId, productId).GetProduct();
        if (product is null)
            return NotFound();
        return Ok(product);
    }

    [HttpPatch]
    [Route("/product")]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult> ProcessPriceUpdate([FromServices] IGrainFactory grains, [FromBody] PriceUpdate update)
    {
        var grain = this.callback(grains, update.sellerId, update.productId);
        try{
            await grain.ProcessPriceUpdate(update);
            return Accepted();
        } catch(Exception e)
        {
            return StatusCode((int)HttpStatusCode.InternalServerError, e.Message);
        }
    }

    [HttpPut]
    [Route("/product")]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult> ProcessUpdateProduct([FromServices] IGrainFactory grains, [FromBody] Product product)
    {
        var grain = this.callback(grains, product.seller_id, product.product_id);

        try{
            await grain.ProcessProductUpdate(product);
            return Accepted();
        } catch(Exception e)
        {
            return StatusCode((int)HttpStatusCode.InternalServerError, e.Message);
        }
    }

    private delegate IProductActor GetProductActorDelegate(IGrainFactory grains, int sellerId, int productId);

    private IProductActor GetProductActor(IGrainFactory grains, int sellerId, int productId)
    {
        return grains.GetGrain<IProductActor>(sellerId, productId.ToString());
    }

    private ITransactionalProductActor GetTransactionalProductActor(IGrainFactory grains, int sellerId, int productId)
    {
        return grains.GetGrain<ITransactionalProductActor>(sellerId, productId.ToString());
    }

}

