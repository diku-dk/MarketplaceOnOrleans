using System.Net;
using Microsoft.AspNetCore.Mvc;
using Orleans.Infra;
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
    [Route("/shipment/{instanceId}")]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    public async Task<ActionResult> UpdateShipment([FromServices] IGrainFactory grains, int instanceId)
    {
        List<Task> tasks = new List<Task>(Constants.NumShipmentActors);
        for(int i = 1; i <= Constants.NumShipmentActors; i++)
        {
            var grain = grains.GetGrain<IShipmentActor>(i);
            tasks.Add(grain.UpdateShipment(instanceId));
        }

        await Task.WhenAll(tasks);
        
        return Accepted();
    }
    
}

