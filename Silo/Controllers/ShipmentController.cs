using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Orleans.Interfaces;

namespace Silo.Controllers;


[ApiController]
public class ShipmentController : ControllerBase
{
    private readonly ILogger<ShipmentController> logger;

    private int numSellers;

    public ShipmentController(ILogger<ShipmentController> logger)
    {
        this.logger = logger;
    }

    [HttpPost]
    [Route("{numSellers}")]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    public ActionResult UpdateShipment(int numSellers)
    {
        this.numSellers = numSellers;
        return Accepted();
    }

    [HttpPatch]
    [Route("{instanceId}")]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    public async Task<ActionResult> UpdateShipment([FromServices] IGrainFactory grains, int instanceId)
    {
        logger.LogDebug("instance id", instanceId);
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

