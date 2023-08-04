using Common.Entities;
using Microsoft.AspNetCore.Mvc;
using Orleans.Interfaces;

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
    public async Task<ActionResult> GetProduct([FromServices] IGrainFactory grains, [FromBody] Product product)
    {
        await grains.GetGrain<IProductActor>(product.seller_id, product.product_id.ToString()).SetProduct(product);
        return Ok();
    }

}

