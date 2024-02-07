using Common.Entities;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using OrleansApp.Interfaces;
using Common.Integration;
using Orleans.Interfaces.SellerView;
using Common.Config;

namespace Silo.Controllers;

[ApiController]
public sealed class SellerController : ControllerBase
{

	private readonly ILogger<SellerController> logger;

    private delegate ISellerActor GetSellerActorDelegate(IGrainFactory grains, int sellerId);
    private readonly GetSellerActorDelegate callback;

    public SellerController(AppConfig config, ILogger<SellerController> logger)
    {
        this.logger = logger;
        this.callback = config.SellerViewPostgres ? GetSellerViewActor : GetSellerActor;
    }

    private ISellerActor GetSellerActor(IGrainFactory grains, int sellerId)
    {
        return grains.GetGrain<ISellerActor>(sellerId);
    }

    private ISellerViewActor GetSellerViewActor(IGrainFactory grains, int sellerId)
    {
        return grains.GetGrain<ISellerViewActor>(sellerId);
    }

    [HttpPost]
    [Route("/seller")]
    [ProducesResponseType((int)HttpStatusCode.Created)]
    public async Task<ActionResult> SetSeller([FromServices] IGrainFactory grains, [FromBody] Seller seller)
    {
        this.logger.LogDebug("[SetSeller] received for id {0}", seller.id);
        var actor = this.callback(grains, seller.id);
        await actor.SetSeller(seller);
        return StatusCode((int)HttpStatusCode.Created);
    }

    [HttpGet]
    [Route("/seller/dashboard/{sellerId}")]
    [ProducesResponseType(typeof(SellerDashboard),(int)HttpStatusCode.OK)]
    public async Task<ActionResult<SellerDashboard>> GetDashboard([FromServices] IGrainFactory grains, int sellerId)
    {
        var actor = this.callback(grains, sellerId);
        var dash = await actor.QueryDashboard();
        return Ok(dash);
    }

    [HttpGet]
    [Route("/seller/{sellerId}")]
    [ProducesResponseType(typeof(Seller),(int)HttpStatusCode.OK)]
    public async Task<ActionResult<Seller>> GetSeller([FromServices] IGrainFactory grains, int sellerId)
    {
        var actor = this.callback(grains, sellerId);
        var Seller = await actor.GetSeller();
        return Ok(Seller);
    }

}

