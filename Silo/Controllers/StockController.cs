using Common.Entities;
using Microsoft.AspNetCore.Mvc;
using Orleans.Interfaces;

namespace Silo.Controllers;

public class StockController : ControllerBase
{
    private readonly ILogger<StockController> logger;

    public StockController(ILogger<StockController> logger)
    {
        this.logger = logger;
    }

    [HttpPost]
    [Route("/stock")]
    public async Task<ActionResult> SetProduct([FromServices] IGrainFactory grains, [FromBody] StockItem item)
    {
        this.logger.LogDebug("[SetStockItem] received for id {0} {1}", item.seller_id, item.product_id);
        await grains.GetGrain<IStockActor>(item.seller_id, item.product_id.ToString()).SetItem(item);
        return Ok();
    }


}

