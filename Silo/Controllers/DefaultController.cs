using System;
using System.Net;
using Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Orleans.Infra;
using Orleans.Interfaces;
using Orleans.Runtime;

namespace Silo.Controllers;

[ApiController]
public class DefaultController : ControllerBase
{
    private readonly IPersistence persistence;
    private readonly AppConfig config;
    private readonly ILogger<DefaultController> logger;

    public DefaultController(IPersistence persistence, AppConfig options, ILogger<DefaultController> logger)
    {
        this.persistence = persistence;
        this.config = options;
        this.logger = logger;
    }

    [Route("/reset")]
    [HttpPatch]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    public async Task<ActionResult> Reset([FromServices] IGrainFactory grains)
    {
        logger.LogWarning("Reset requested at {0}", DateTime.UtcNow);

        // SimpleGrainStatistic
        var mgmt = grains.GetGrain<IManagementGrain>(0);
        var stats = await mgmt.GetSimpleGrainStatistics();

        // get sellers and orders actors to reset
        // cannot get orders and sellers from shipments
        // because some of them may have been already removed from memory
        foreach(var stat in stats)
        {
            logger.LogDebug("{stat}",stat.ToString());
            if (stat.GrainType.SequenceEqual("Orleans.Grains.OrderActor,Orleans"))
            {
                int num = stat.ActivationCount;
                var tasks = new List<Task>();
                for(int i = 1; i <= num; i++)
                {
                    tasks.Add( grains.GetGrain<IOrderActor>(i).Reset() );
                }
                await Task.WhenAll(tasks);
                logger.LogWarning("{0} order states resetted", num);
                continue;
            }
            if (stat.GrainType.SequenceEqual("Orleans.Grains.SellerActor,Orleans"))
            {
                int num = stat.ActivationCount;
                var tasks = new List<Task>();
                for(int i = 1; i <= num; i++)
                {
                    tasks.Add( grains.GetGrain<ISellerActor>(i).Reset() );
                }
                await Task.WhenAll(tasks);
                logger.LogWarning("{0} seller states resetted", num);
                continue;
            }
            // seal carts that have not checked out in past run
            if (stat.GrainType.SequenceEqual("Orleans.Grains.CartActor,Orleans"))
            {
                int num = stat.ActivationCount;
                var tasks = new List<Task>();
                for(int i = 1; i <= num; i++)
                {
                    tasks.Add( grains.GetGrain<ICartActor>(i).Seal() );
                }
                await Task.WhenAll(tasks);
                logger.LogWarning("{0} cart states resetted", num);
            }
            if (stat.GrainType.SequenceEqual("Orleans.Grains.StockActor,Orleans"))
            {
                int num = stat.ActivationCount;
                var tasks = new List<Task>();
                for(int i = 1; i <= num; i++)
                {
                    for(int j = 1; j <= 10; j++)
                        tasks.Add( grains.GetGrain<IStockActor>(i,j.ToString()).Reset() );
                }
                await Task.WhenAll(tasks);
                logger.LogWarning("{0} stock states resetted", num);
            }
            if (stat.GrainType.SequenceEqual("Orleans.Grains.ProductActor,Orleans"))
            {
                int num = stat.ActivationCount;
                var tasks = new List<Task>();
                for(int i = 1; i <= num; i++)
                {
                    for(int j = 1; j <= 10; j++)
                        tasks.Add( grains.GetGrain<IProductActor>(i,j.ToString()).Reset() );
                }
                await Task.WhenAll(tasks);
                logger.LogWarning("{0} product states resetted", num);
            }
        }

        // Helper.CleanLog();
        await persistence.CleanLog();
        await ResetShipmentActors(grains);

        return Ok();
    }

    private async Task ResetShipmentActors(IGrainFactory grains)
    {
        List<Task> tasks = new List<Task>(config.NumShipmentActors);
        for(int i = 0; i < config.NumShipmentActors; i++)
        {
            var grain = grains.GetGrain<IShipmentActor>(i);
            tasks.Add(grain.Reset());
        }
        await Task.WhenAll(tasks);
        logger.LogWarning("{0} shipment states resetted", config.NumShipmentActors);
    }

    // should be called before shutting off the app server
    [Route("/cleanup")]
    [HttpPatch]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    public async Task<ActionResult> Cleanup()
    {
        this.logger.LogWarning("Cleanup requested at {0}", DateTime.UtcNow);
        // Helper.TruncateOrleansStorage();
        await persistence.TruncateStorage();
        // Helper.CleanLog();
        await persistence.CleanLog();
        return Ok();
    }

}