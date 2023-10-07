using System.Net;
using Common;
using Common.Entities;
using Common.Requests;
using Microsoft.AspNetCore.Mvc;
using Orleans.Interfaces;

namespace Orleans.Controllers;

[ApiController]
public class ProductController : ControllerBase
{
    private readonly ILogger<ProductController> logger;
    private readonly string grainClassNamePrefix;

    public ProductController(AppConfig config, ILogger<ProductController> logger)
    {
        this.grainClassNamePrefix = config.OrleansTransactions ? "Orleans.Transactional.TransactionalProductActor" :
            "Orleans.Grains.ProductActor";
        this.logger = logger;
    }

    [HttpPost]
    [Route("/product")]
    public async Task<ActionResult> SetProduct([FromServices] IGrainFactory grains, [FromBody] Product product)
    {
        this.logger.LogDebug("[SetProduct] received for id {0} {1}", product.seller_id, product.product_id);
        await grains.GetGrain<IProductActor>(product.seller_id, product.product_id.ToString(), grainClassNamePrefix).SetProduct(product);
        return Ok();
    }

    [HttpGet("/product/{sellerId:long}/{productId:long}")]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(Product), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<Product>> GetBySellerIdAndProductId([FromServices] IGrainFactory grains, int sellerId, int productId)
    {
        var grain = grains.GetGrain<IProductActor>(sellerId, productId.ToString(), grainClassNamePrefix);
        var product = await grain.GetProduct();
        if (product is null)
            return NotFound();
        return Ok(product);
    }

    [HttpPatch]
    [Route("/product")]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    public async Task<ActionResult> ProcessPriceUpdate([FromServices] IGrainFactory grains, [FromBody] PriceUpdate update)
    {
        var grain = grains.GetGrain<IProductActor>(update.sellerId, update.productId.ToString(), grainClassNamePrefix);
        await grain.ProcessPriceUpdate(update);
        return Accepted();
    }

    [HttpPut]
    [Route("/product")]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    public async Task<ActionResult> ProcessUpdateproduct([FromServices] IGrainFactory grains, [FromBody] Product product)
    {
        var grain = grains.GetGrain<IProductActor>(product.seller_id, product.product_id.ToString(), grainClassNamePrefix);
        await grain.ProcessProductUpdate(product);
        return Accepted();
    }
}

