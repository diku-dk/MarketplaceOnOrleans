using System.Net;
using Common.Config;
using Common.Entities;
using Microsoft.AspNetCore.Mvc;
using OrleansApp.Interfaces;
using OrleansApp.Transactional;

namespace Silo.Controllers;

[ApiController]
public sealed class OrderController : ControllerBase
{
    private readonly ILogger<OrderController> logger;
    private readonly bool OrleansTransactions;
    private readonly ITransactionClient transactionClient;

    public OrderController(AppConfig config, ITransactionClient transactionClient, ILogger<OrderController> logger)
    {
        this.logger = logger;
        this.OrleansTransactions = config.OrleansTransactions;
        this.transactionClient = transactionClient;
    }

    [HttpGet("/order/{customerId}")]
    [ProducesResponseType(typeof(IEnumerable<Order>), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<IEnumerable<Order>>> GetByCustomerId([FromServices] IGrainFactory grainFactory, int customerId)
    {
        List<Order> orders = null;     
        if(this.OrleansTransactions){
            var grain = this.GetTxOrderActor(grainFactory, customerId);
            await this.transactionClient.RunTransaction(
                TransactionOption.Create,
                async () =>
                {
                    orders = await grain.GetOrders();
                });
        }
        else
        {
            var grain = GetDefaultOrderActor(grainFactory, customerId);
            orders = await grain.GetOrders();
        }
        if (orders is null)
            return NotFound();
        return Ok(orders);
    }

    private IOrderActor GetDefaultOrderActor(IGrainFactory grains, int customerId)
    {
        return grains.GetGrain<IOrderActor>(customerId);
    }

    private ITransactionalOrderActor GetTxOrderActor(IGrainFactory grains, int customerId)
    {
        return grains.GetGrain<ITransactionalOrderActor>(customerId);
    }

}