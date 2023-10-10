using System.Net;
using Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Orleans.Infra;
using Orleans.Interfaces;
using Orleans.Transactional;

namespace Silo.Controllers;

[ApiController]
public class ShipmentController : ControllerBase
{
    private readonly AppConfig config;
    private readonly ILogger<ShipmentController> logger;
    private readonly GetShipmentActorDelegate callback;

    private delegate IShipmentActor GetShipmentActorDelegate(IGrainFactory grains, int partitionId);

    private IShipmentActor GetShipmentActor(IGrainFactory grains, int partitionId)
    {
        return grains.GetGrain<IShipmentActor>(partitionId);
    }

    private ITransactionalShipmentActor GetTransactionalShipmentActor(IGrainFactory grains, int partitionId)
    {
        return grains.GetGrain<ITransactionalShipmentActor>(partitionId);
    }

    public ShipmentController(AppConfig options, ILogger<ShipmentController> logger)
    {
        this.config = options;
        this.logger = logger;
        this.callback = config.OrleansTransactions ? GetTransactionalShipmentActor : GetShipmentActor;
    }

    [HttpPatch]
    [Route("/shipment/{instanceId}")]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    public async Task<ActionResult> UpdateShipment([FromServices] IGrainFactory grains, string instanceId)
    {
        List<Task> tasks = new List<Task>(config.NumShipmentActors);
        try{
            for(int i = 0; i < config.NumShipmentActors; i++)
            {
                var grain = this.callback(grains, i);
                tasks.Add(grain.UpdateShipment(instanceId));
            }

            await Task.WhenAll(tasks);
            return Accepted();
        } catch(Exception e)
        {
            return StatusCode((int)HttpStatusCode.InternalServerError, e.Message);
        }
        
    }
    
}

