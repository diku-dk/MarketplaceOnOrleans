using System.Net;
using Microsoft.AspNetCore.Mvc;
using Orleans.Interfaces;

namespace Order.Controllers;

[ApiController]
public class OrderController : ControllerBase
{
    private readonly ILogger<OrderController> logger;

    public OrderController(ILogger<OrderController> logger)
    {
        this.logger = logger;
    }

    [HttpGet("/order/{customerId}")]
    [ProducesResponseType(typeof(IEnumerable<Common.Entities.Order>), (int)HttpStatusCode.OK)]
    public ActionResult<IEnumerable<Common.Entities.Order>> GetByCustomerId([FromServices] IGrainFactory grains, int customerId)
    {
        return Ok(grains.GetGrain<IOrderActor>(customerId).GetOrders());
    }

}