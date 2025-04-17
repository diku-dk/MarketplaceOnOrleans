using System.Net;
using Microsoft.AspNetCore.Mvc;
using OrleansApp.Infra;
using OrleansApp.Interfaces;
using Common.Config;
using OrleansApp.Service;

namespace Silo.Controllers;

[ApiController]
public sealed class DefaultController : ControllerBase
{
    private readonly IAuditLogger persistence;
    private readonly AppConfig config;
    private readonly IShipmentService shipmentService;
    private readonly ILogger<DefaultController> logger;

    public DefaultController(IAuditLogger persistence, AppConfig options, IShipmentService shipmentService, ILogger<DefaultController> logger)
    {
        this.persistence = persistence;
        this.config = options;
        this.shipmentService = shipmentService;
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
            this.logger.LogDebug("{stat}",stat.ToString());
            if (stat.GrainType.SequenceEqual("OrleansApp.Grains.OrderActor,Orleans"))
            {
                int num = stat.ActivationCount;
                var tasks = new List<Task>();
                for(int i = 1; i <= num; i++)
                {
                    tasks.Add( grains.GetGrain<IOrderActor>(i).Reset() );
                }
                await Task.WhenAll(tasks);
                this.logger.LogWarning("{0} order states reset", num);
                continue;
            }
            if (stat.GrainType.SequenceEqual("OrleansApp.Grains.SellerActor,Orleans"))
            {
                int num = stat.ActivationCount;
                var tasks = new List<Task>();
                for(int i = 1; i <= num; i++)
                {
                    tasks.Add( grains.GetGrain<ISellerActor>(i).Reset() );
                }
                await Task.WhenAll(tasks);
                this.logger.LogWarning("{0} seller states reset", num);
                continue;
            }
            // seal carts that have not checked out in past run
            if (stat.GrainType.SequenceEqual("OrleansApp.Grains.CartActor,Orleans"))
            {
                int num = stat.ActivationCount;
                var tasks = new List<Task>();
                for(int i = 1; i <= num; i++)
                {
                    tasks.Add( grains.GetGrain<ICartActor>(i).Seal() );
                }
                await Task.WhenAll(tasks);
                this.logger.LogWarning("{0} cart states reset", num);
            }
            if (stat.GrainType.SequenceEqual("OrleansApp.Grains.StockActor,Orleans"))
            {
                int num = stat.ActivationCount;
                var tasks = new List<Task>();
                for(int i = 1; i <= num; i++)
                {
                    for(int j = 1; j <= 10; j++)
                        tasks.Add( grains.GetGrain<IStockActor>(i,j.ToString()).Reset() );
                }
                await Task.WhenAll(tasks);
                this.logger.LogWarning("{0} stock states reset", num);
            }
            if (stat.GrainType.SequenceEqual("OrleansApp.Grains.ProductActor,Orleans"))
            {
                int num = stat.ActivationCount;
                var tasks = new List<Task>();
                for(int i = 1; i <= num; i++)
                {
                    for(int j = 1; j <= 10; j++)
                        tasks.Add( grains.GetGrain<IProductActor>(i,j.ToString()).Reset() );
                }
                await Task.WhenAll(tasks);
                this.logger.LogWarning("{0} product states reset", num);
            }
        }
        await this.persistence.CleanLog();
        await this.shipmentService.ResetShipmentActors();
        return Ok();
    }

    /*
     * Should be called before shutting off the app server, right after an experiment run
     */
    [Route("/cleanup")]
    [HttpPatch]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    public async Task<ActionResult> Cleanup()
    {
        this.logger.LogWarning("Cleanup requested at {0}", DateTime.UtcNow);
        if (config.LogRecords)
        {
            await persistence.CleanLog();
        }
        if (config.AdoNetGrainStorage)
        {
            await persistence.TruncateStorage();
        }
        if (config.SellerViewPostgres)
        {
            await persistence.ExecuteSqlCommand("TRUNCATE TABLE public.order_entries;");
        }
        return Ok();
    }

    [Route("/status")]
    [HttpGet]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    public async Task<ActionResult> Status([FromServices] IGrainFactory grains)
    {
        this.logger.LogDebug("Status requested at {0}", DateTime.UtcNow);
        var managementGrain = grains.GetGrain<IManagementGrain>(0);
        var stats = await managementGrain.GetDetailedGrainStatistics();
        foreach (var stat in stats)
        {
            if (stat.GrainType.Contains("OrleansApp.Grains"))
            {
                Console.WriteLine($"GrainType: {stat.GrainType}, GrainId: {stat.GrainId}, Silo: {stat.SiloAddress}");
            }
        }
        return Ok();
    }

}