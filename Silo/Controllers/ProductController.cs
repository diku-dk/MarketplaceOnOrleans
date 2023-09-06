using System.Net;
using Common.Entities;
using Common.Requests;
using Microsoft.AspNetCore.Mvc;
using Orleans.Interfaces;

namespace Orleans.Controllers;

[ApiController]
public class ProductController : ControllerBase
{
    private readonly ILogger<ProductController> logger;

    public ProductController(ILogger<ProductController> logger)
    {
        this.logger = logger;
    }

    [HttpPost]
    [Route("/product")]
    public async Task<ActionResult> SetProduct([FromServices] IGrainFactory grains, [FromBody] Product product)
    {
        await grains.GetGrain<IProductActor>(product.seller_id, product.product_id.ToString()).SetProduct(product);
        return Ok();
    }

    [HttpGet("/product/{sellerId:long}/{productId:long}")]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(Product), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<Product>> GetBySellerIdAndProductId([FromServices] IGrainFactory grains, int sellerId, int productId)
    {
        var grain = grains.GetGrain<IProductActor>(sellerId, productId.ToString());
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
        var grain = grains.GetGrain<IProductActor>(update.sellerId, update.productId.ToString());
        await grain.ProcessPriceUpdate(update);
        return Accepted();
    }

    [HttpPut]
    [Route("/product")]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    public async Task<ActionResult> ProcessUpdateproduct([FromServices] IGrainFactory grains, [FromBody] Product product)
    {
        var grain = grains.GetGrain<IProductActor>(product.seller_id, product.product_id.ToString());
        await grain.ProcessProductUpdate(product);
        return Accepted();
    }
}

