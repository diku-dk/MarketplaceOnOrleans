using System;
using Common.Entities;
using Microsoft.AspNetCore.Mvc;
using Orleans.Controllers;
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
        await grains.GetGrain<IStockActor>(item.seller_id, item.product_id.ToString()).SetItem(item);
        return Ok();
    }


}

