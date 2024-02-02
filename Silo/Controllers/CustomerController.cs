using Common.Entities;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using OrleansApp.Interfaces;

namespace Silo.Controllers;

[ApiController]
public sealed class CustomerController : ControllerBase
{

	private readonly ILogger<SellerController> logger;

    public CustomerController(ILogger<SellerController> logger)
    {
        this.logger = logger;
    }

    [HttpPost("/customer")]
    [ProducesResponseType((int)HttpStatusCode.Created)]
    public async Task<ActionResult> AddCustomer([FromServices] IGrainFactory grains, [FromBody] Customer customer)
    {
        this.logger.LogDebug("[AddCustomer] received for id {0}", customer.id);
        var grain = grains.GetGrain<ICustomerActor>(customer.id);
        await grain.SetCustomer( customer );
        return StatusCode((int)HttpStatusCode.Created);
    }

    [HttpGet("/customer/{customerId}")]
    [ProducesResponseType(typeof(Customer), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<Customer>> GetCustomerById([FromServices] IGrainFactory grains, int customerId)
    {
        this.logger.LogDebug("[GetCustomerById] received for id {0}", customerId);
        var grain = grains.GetGrain<ICustomerActor>(customerId);
        var cust = await grain.GetCustomer();
        return Ok(cust);
    }

}

