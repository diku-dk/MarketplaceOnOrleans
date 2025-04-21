using System.Net;
using Common.Config;
using Common.Entities;
using Microsoft.AspNetCore.Mvc;
using OrleansApp.Interfaces;
using OrleansApp.Transactional;

namespace Silo.Controllers;

public sealed class StockController : ControllerBase
{
    private readonly ILogger<StockController> logger;

    private readonly bool OrleansTransactions;

    private readonly ITransactionClient transactionClient;

    public StockController(AppConfig config, IHost host, ILogger<StockController> logger)
    {
        this.logger = logger;
        this.OrleansTransactions = config.OrleansTransactions;
        this.transactionClient = config.OrleansTransactions ? host.Services.GetRequiredService<ITransactionClient>() : null;
    }

    [HttpPost]
    [Route("/stock")]
    public async Task<ActionResult> AddItem([FromServices] IGrainFactory grainFactory, [FromBody] StockItem item)
    {
        this.logger.LogDebug("[AddItem] for ID {0}|{1}", item.seller_id, item.product_id);
        if(this.OrleansTransactions){
            var grain = this.GetTxStockActor(grainFactory, item.seller_id, item.product_id);
            await this.transactionClient.RunTransaction(
            TransactionOption.Create,
            async () =>
            {
                await grain.SetItem(item);
            });
        }
        else
        {
            var grain = GetDefaultStockActor(grainFactory, item.seller_id, item.product_id);
            await grain.SetItem(item);
        }
        return Ok();
    }

    [HttpGet("/stock/{sellerId:long}/{productId:long}")]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(StockItem), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<StockItem>> GetBySellerIdAndProductId([FromServices] IGrainFactory grainFactory, int sellerId, int productId)
    {
        StockItem item = null;
        if(this.OrleansTransactions){
            var grain = this.GetTxStockActor(grainFactory, sellerId, productId);
            await this.transactionClient.RunTransaction(
                TransactionOption.Create,
                async () =>
                {
                    item = await grain.GetItem();
                });
        }
        else
        {
            var grain = GetDefaultStockActor(grainFactory, sellerId, productId);
            item = await grain.GetItem();
        }
        if (item is null)
            return NotFound();
        return Ok(item);
    }

    private IStockActor GetDefaultStockActor(IGrainFactory grains, int sellerId, int productId)
    {
        return grains.GetGrain<IStockActor>(sellerId, productId.ToString());
    }

    private ITransactionalStockActor GetTxStockActor(IGrainFactory grains, int sellerId, int productId)
    {
        return grains.GetGrain<ITransactionalStockActor>(sellerId, productId.ToString());
    }

}

