using System.Net;
using Microsoft.AspNetCore.Mvc;
using OrleansApp.Service;

namespace Silo.Controllers;

[ApiController]
public sealed class ShipmentController : ControllerBase
{
    private readonly IShipmentService shipmentService;
    private readonly ILogger<ShipmentController> logger;

    public ShipmentController(IShipmentService shipmentService, ILogger<ShipmentController> logger)
    {
        this.shipmentService = shipmentService;
        this.logger = logger;
    }

    [HttpPatch]
    [Route("/shipment/{instanceId}")]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    [ProducesResponseType((int)HttpStatusCode.InternalServerError)]
    public async Task<ActionResult> UpdateShipment(string instanceId)
    {
        try{
            await this.shipmentService.UpdateShipment(instanceId);
            return Accepted();
        } catch(Exception e)
        {
            return StatusCode((int)HttpStatusCode.InternalServerError, e.Message);
        }
        
    }
    
}

