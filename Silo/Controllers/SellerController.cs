using Common.Entities;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Orleans.Interfaces;

namespace Silo.Controllers;

[ApiController]
public class SellerController : ControllerBase
{

	private readonly ILogger<SellerController> logger;

    public SellerController(ILogger<SellerController> logger)
    {
        this.logger = logger;
    }

    [HttpPost]
    [Route("seller/")]
    [ProducesResponseType((int)HttpStatusCode.Created)]
    public async Task<ActionResult> AddSeller([FromServices] IGrainFactory grains, [FromBody] Seller seller)
    {
        var actor = grains.GetGrain<ISellerActor>(seller.id);
        await actor.SetSeller(seller);
        return StatusCode((int)HttpStatusCode.Created);
    }

    [Route("seller/reset")]
    [HttpPatch]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    public ActionResult Reset([FromServices] IGrainFactory grains)
    {
        logger.LogWarning("Reset requested at {0}", DateTime.UtcNow);
        return Ok();
    }

    [Route("seller/cleanup")]
    [HttpPatch]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    public ActionResult Cleanup()
    {
        logger.LogWarning("Cleanup requested at {0}", DateTime.UtcNow);
        return Ok();
    }

}

