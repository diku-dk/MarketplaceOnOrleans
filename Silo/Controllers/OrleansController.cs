using System.Net;
using Common.Entities;
using Common.Requests;
using Microsoft.AspNetCore.Mvc;
using Orleans.Interfaces;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Orleans.Controllers;

[ApiController]
public class OrleansController : ControllerBase
{
    private readonly ILogger<OrleansController> logger;

    public OrleansController(ILogger<OrleansController> logger)
    {
        this.logger = logger;
    }

    [HttpGet]
    [Route("/")]
    public async Task<ActionResult<string>> Get([FromServices] IGrainFactory grains)
    {
        return Ok( await grains.GetGrain<IPersistentGrain>(0).GetUrl() );
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
    public async Task<ActionResult> UpdateProduct([FromServices] IGrainFactory grains, [FromBody] UpdatePrice update)
    {
        var grain = grains.GetGrain<IProductActor>(update.sellerId, update.productId.ToString());
        await grain.UpdatePrice(update);
        return Accepted();
    }

    [HttpDelete]
    [Route("/product")]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    public async Task<ActionResult> DeleteProduct([FromServices] IGrainFactory grains, [FromBody] DeleteProduct deleteProduct)
    {
        var grain = grains.GetGrain<IProductActor>(deleteProduct.sellerId, deleteProduct.productId.ToString());
        await grain.DeleteProduct(deleteProduct);
        return Accepted();
    }
}

