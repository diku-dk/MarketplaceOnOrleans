using System.Net;
using Common.Config;
using Microsoft.AspNetCore.Mvc;
using OrleansApp.Interfaces;
using OrleansApp.Transactional;

namespace Silo.Controllers;

[ApiController]
public sealed class OrderController : ControllerBase
{
    private readonly ILogger<OrderController> logger;
    private readonly GetOrderActorDelegate callback;

    private delegate IOrderActor GetOrderActorDelegate(IGrainFactory grains, int customerId);

    public OrderController(AppConfig config, ILogger<OrderController> logger)
    {
        this.logger = logger;
        this.callback = config.OrleansTransactions ? GetTransactionalOrderActor : GetOrderActor;
    }

    [HttpGet("/order/{customerId}")]
    [ProducesResponseType(typeof(IEnumerable<Common.Entities.Order>), (int)HttpStatusCode.OK)]
    public ActionResult<IEnumerable<Common.Entities.Order>> GetByCustomerId([FromServices] IGrainFactory grains, int customerId)
    {
        return Ok(this.callback(grains,customerId).GetOrders());
    }

    private IOrderActor GetOrderActor(IGrainFactory grains, int customerId)
    {
        return grains.GetGrain<IOrderActor>(customerId);
    }

    private ITransactionalOrderActor GetTransactionalOrderActor(IGrainFactory grains, int customerId)
    {
        return grains.GetGrain<ITransactionalOrderActor>(customerId);
    }

}