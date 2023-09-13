using Common.Entities;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Orleans.Interfaces;

namespace Silo.Controllers;

[ApiController]
public class CustomerController : ControllerBase
{

	private readonly ILogger<SellerController> logger;

    public CustomerController(ILogger<SellerController> logger)
    {
        this.logger = logger;
    }

    [HttpPost("/customer")]
    [ProducesResponseType((int)HttpStatusCode.Created)]
    public ActionResult AddCustomer([FromServices] IGrainFactory grains, [FromBody] Customer customer)
    {
        this.logger.LogDebug("[AddCustomer] received for id {0}", customer.id);
        var grain = grains.GetGrain<ICustomerActor>(customer.id);
        grain.SetCustomer( customer );
        return StatusCode((int)HttpStatusCode.Created);
    }

}

