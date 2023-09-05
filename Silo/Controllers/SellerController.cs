using Common.Entities;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Orleans.Interfaces;
using Orleans.Infra;

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
    public ActionResult AddSeller([FromServices] IGrainFactory grains, [FromBody] Seller seller)
    {
        var actor = grains.GetGrain<IRegistrarActor>(seller.id);
        actor.AddSeller(seller);
        return StatusCode((int)HttpStatusCode.Created);
    }

    [Route("seller/reset")]
    [HttpPatch]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    public async Task<ActionResult> Reset([FromServices] IGrainFactory grains)
    {
        logger.LogWarning("Reset requested at {0}", DateTime.UtcNow);
        var actor = grains.GetGrain<IRegistrarActor>(0);
        await actor.Reset();
        return Ok();
    }

    [Route("seller/cleanup")]
    [HttpPatch]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    public ActionResult Cleanup()
    {
        logger.LogWarning("Cleanup requested at {0}", DateTime.UtcNow);
        // this.sellerService.Cleanup();
        return Ok();
    }

}

