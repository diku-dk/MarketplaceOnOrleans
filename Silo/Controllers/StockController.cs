using System.Net;
using Common;
using Common.Entities;
using Microsoft.AspNetCore.Mvc;
using OrleansApp.Interfaces;
using OrleansApp.Transactional;

namespace Silo.Controllers;

public sealed class StockController : ControllerBase
{
    private readonly ILogger<StockController> logger;

    private delegate IStockActor GetStockActorDelegate(IGrainFactory grains, int sellerId, int productId);

    private readonly GetStockActorDelegate callback;

    public StockController(AppConfig config, ILogger<StockController> logger)
    {
        this.logger = logger;
        this.callback = config.OrleansTransactions ? GetTransactionalStockActor : GetStockActor;
    }

    [HttpPost]
    [Route("/stock")]
    public async Task<ActionResult> AddItem([FromServices] IGrainFactory grains, [FromBody] StockItem item)
    {
        this.logger.LogDebug("[AddItem] received for id {0} {1}", item.seller_id, item.product_id);
        await this.callback(grains, item.seller_id, item.product_id).SetItem(item);
        return Ok();
    }

    [HttpGet("/stock/{sellerId:long}/{productId:long}")]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(StockItem), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<StockItem>> GetBySellerIdAndProductId([FromServices] IGrainFactory grains, int sellerId, int productId)
    {
        var grain = this.callback(grains, sellerId, productId);
        var item = await grain.GetItem();
        return Ok(item);
    }

    private IStockActor GetStockActor(IGrainFactory grains, int sellerId, int productId)
    {
        return grains.GetGrain<IStockActor>(sellerId, productId.ToString());
    }

    private ITransactionalStockActor GetTransactionalStockActor(IGrainFactory grains, int sellerId, int productId)
    {
        return grains.GetGrain<ITransactionalStockActor>(sellerId, productId.ToString());
    }

}

