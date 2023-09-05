using System.Net;
using Microsoft.AspNetCore.Mvc;
using Orleans.Interfaces;

namespace Silo.Controllers;


[ApiController]
public class ShipmentController : ControllerBase
{
    private readonly ILogger<ShipmentController> logger;

    public ShipmentController(ILogger<ShipmentController> logger)
    {
        this.logger = logger;
    }

    [HttpPatch]
    [Route("shipment/{instanceId}")]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    public async Task<ActionResult> UpdateShipment([FromServices] IGrainFactory grains, int instanceId)
    {
        logger.LogDebug("instance id", instanceId);
        var registrar = grains.GetGrain<IRegistrarActor>(0);
        int numSellers = await registrar.GetNumSellers();
        List<Task> tasks = new List<Task>(numSellers);
        for(int i = 1; i <= numSellers; i++)
        {
            var grain = grains.GetGrain<IShipmentActor>(1);
            tasks.Add(grain.UpdateShipment());
        }

        await Task.WhenAll(tasks);
        
        return Accepted();
    }
    
}

