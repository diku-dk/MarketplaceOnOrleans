using System;
using Common.Entities;
using Common.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Net;
using Orleans.Controllers;

namespace Silo.Controllers;


[ApiController]
public class CartController : ControllerBase
{

    private readonly ILogger<OrleansController> logger;

    public CartController(ILogger<OrleansController> logger)
    {
        this.logger = logger;
    }


    [Route("/cart/{customerId}/add")]
    [HttpPatch]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    [ProducesResponseType((int)HttpStatusCode.MethodNotAllowed)]
    [ProducesResponseType((int)HttpStatusCode.Conflict)]
    public ActionResult AddItem(long customerId, [FromBody] CartItem item)
    {
        return null;
    }

    [Route("/cart/{customerId}/checkout")]
    [HttpPost]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    [ProducesResponseType(typeof(Cart), (int)HttpStatusCode.MethodNotAllowed)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult> NotifyCheckout(long customerId, [FromBody] CustomerCheckout customerCheckout)
    {
        return null;
        /*
        this.logger.LogInformation("[NotifyCheckout] received request.");

        if (customerId != customerCheckout.CustomerId)
        {
            logger.LogError("Customer checkout payload ({0}) does not match customer ID ({1}) in URL", customerId, customerCheckout.CustomerId);
            return StatusCode((int)HttpStatusCode.MethodNotAllowed, "Customer checkout payload does not match customer ID in URL");
        }

        Cart cart = this.cartRepository.GetCart(customerCheckout.CustomerId);

        if (cart is null)
        {
            this.logger.LogWarning("Customer {0} cart cannot be found", customerCheckout.CustomerId);
            return NotFound("Customer " + customerCheckout.CustomerId + " cart cannot be found");
        }

        if (cart.status == CartStatus.CHECKOUT_SENT)
        {
            this.logger.LogWarning("Customer {0} cart has already been submitted to checkout", customerCheckout.CustomerId);
            return StatusCode((int)HttpStatusCode.MethodNotAllowed, "Customer " + customerCheckout.CustomerId + " cart has already been submitted for checkout");
        }

        var items = this.cartRepository.GetItems(customerCheckout.CustomerId);
        if (items is null || items.Count() == 0)
        {
            return StatusCode((int)HttpStatusCode.MethodNotAllowed, "Customer " + customerCheckout.CustomerId + " cart has no items to be submitted for checkout");
        }

        List<ProductStatus> divergencies = this.cartService.CheckCartForDivergencies(cart);
        if (divergencies.Count() > 0)
        {
            return StatusCode((int)HttpStatusCode.MethodNotAllowed, new Cart()
            {
                customerId = cart.customer_id,
                // items = cartItems,
                status = cart.status,
                divergencies = divergencies
            });
        }

        await this.cartService.NotifyCheckout(customerCheckout, cart);
        return Ok();
        */
    }
}


