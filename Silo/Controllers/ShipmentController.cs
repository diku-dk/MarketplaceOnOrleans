using System.Net;
using Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Orleans.Infra;
using Orleans.Interfaces;

namespace Silo.Controllers;

[ApiController]
public class ShipmentController : ControllerBase
{
    private readonly AppConfig config;
    private readonly ILogger<ShipmentController> logger;

    public ShipmentController(AppConfig options, ILogger<ShipmentController> logger)
    {
        this.config = options;
        this.logger = logger;
    }

    [HttpPatch]
    [Route("/shipment/{instanceId}")]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    public async Task<ActionResult> UpdateShipment([FromServices] IGrainFactory grains, string instanceId)
    {
        List<Task> tasks = new List<Task>(config.NumShipmentActors);
        for(int i = 0; i < config.NumShipmentActors; i++)
        {
            var grain = grains.GetGrain<IShipmentActor>(i);
            tasks.Add(grain.UpdateShipment(instanceId));
        }

        await Task.WhenAll(tasks);
        
        return Accepted();
    }
    
}

