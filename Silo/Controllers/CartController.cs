using Common.Entities;
using Common.Requests;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using OrleansApp.Interfaces;

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
    public async Task<ActionResult> AddItem([FromServices] IGrainFactory grains, long customerId, [FromBody] CartItem item)
    {
        var cartGrain = grains.GetGrain<ICartActor>(customerId);
        await cartGrain.AddItem(item);
        return Ok();
    }

    [Route("/cart/{customerId}/checkout")]
    [HttpPost]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult> NotifyCheckout([FromServices] IGrainFactory grains, long customerId, [FromBody] CustomerCheckout customerCheckout)
    {
        var cartGrain = grains.GetGrain<ICartActor>(customerId);
        try{
            await cartGrain.NotifyCheckout(customerCheckout);
            return Ok();
        } catch(Exception e)
        {
            return StatusCode((int)HttpStatusCode.InternalServerError, e.Message);
        }
    }

    [Route("/cart/{customerId}/seal")]
    [HttpPatch]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult> Seal([FromServices] IGrainFactory grains, int customerId)
    {
        var cartGrain = grains.GetGrain<ICartActor>(customerId);
        try 
        {
            await cartGrain.Seal();
            return Ok();
        }
        catch (Exception e)
        {
            return StatusCode((int)HttpStatusCode.InternalServerError, e.Message);
        }
    }
}

