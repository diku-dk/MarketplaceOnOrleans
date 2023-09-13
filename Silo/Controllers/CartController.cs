using Common.Entities;
using Common.Requests;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using Orleans.Interfaces;

namespace Silo.Controllers;

[ApiController]
public class CartController : ControllerBase
{
    private readonly ILogger<CartController> logger;

    public CartController(ILogger<CartController> logger)
    {
        this.logger = logger;
    }

    [Route("/cart/{customerId}/add")]
    [HttpPatch]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    [ProducesResponseType((int)HttpStatusCode.MethodNotAllowed)]
    [ProducesResponseType((int)HttpStatusCode.Conflict)]
    public async Task<ActionResult> AddItem([FromServices] IGrainFactory grains, long customerId, [FromBody] CartItem item)
    {
        var cartGrain = grains.GetGrain<ICartActor>(customerId);
        await cartGrain.AddItem(item);
        return Ok();
    }

    [Route("/cart/{customerId}/checkout")]
    [HttpPost]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    [ProducesResponseType(typeof(Cart), (int)HttpStatusCode.MethodNotAllowed)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult> NotifyCheckout([FromServices] IGrainFactory grains, long customerId, [FromBody] CustomerCheckout customerCheckout)
    {
        this.logger.LogDebug("[NotifyCheckout] received request for customer id {0}: {1} ",customerId,  customerCheckout.CustomerId);

        // use customerId as cartGrainId and orderGrainId
        var cartGrain = grains.GetGrain<ICartActor>(customerId);
        try 
        {
            await cartGrain.NotifyCheckout(customerCheckout);
            return Ok();
        }
        catch (Exception e)
        {
            return StatusCode((int)HttpStatusCode.MethodNotAllowed, e.Message);
        }
    }
}


