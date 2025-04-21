using System.Net;
using Microsoft.AspNetCore.Mvc;
using OrleansApp.Infra;
using OrleansApp.Interfaces;
using Common.Config;
using OrleansApp.Service;
using OrleansApp.Transactional;

namespace Silo.Controllers;

[ApiController]
public sealed class DefaultController : ControllerBase
{
    private readonly IAuditLogger persistence;
    private readonly AppConfig config;
    private readonly IShipmentService shipmentService;
    private readonly ITransactionClient transactionClient;
    private readonly ILogger<DefaultController> logger;

    public DefaultController(IAuditLogger persistence, AppConfig options, IShipmentService shipmentService, ITransactionClient transactionClient, ILogger<DefaultController> logger)
    {
        this.persistence = persistence;
        this.config = options;
        this.shipmentService = shipmentService;
        this.transactionClient = transactionClient;
        this.logger = logger;
    }

    [Route("/reset")]
    [HttpPatch]
    [ProducesResponseType((int)HttpStatusCode.Accepted)]
    public async Task<ActionResult> Reset([FromServices] IGrainFactory grains)
    {
        this.logger.LogWarning("Reset requested at {0}", DateTime.UtcNow);

        // SimpleGrainStatistic
        var mgmt = grains.GetGrain<IManagementGrain>(0);
        var stats = await mgmt.GetSimpleGrainStatistics();

        var dStats = await mgmt.GetDetailedGrainStatistics();
        var tasks = new List<Task>();

        foreach (var stat in dStats)
        {
            if (stat.GrainType.SequenceEqual("OrleansApp.Transactional.TransactionalStockActor,OrleansApp"))
            {
                Task t = this.transactionClient.RunTransaction(TransactionOption.Create, () =>
                    grains.GetGrain<ITransactionalStockActor>(stat.GrainId).Reset());
                tasks.Add(t);
            }
            if (stat.GrainType.SequenceEqual("OrleansApp.Transactional.TransactionalProductActor,OrleansApp"))
            {
                Task t = this.transactionClient.RunTransaction(TransactionOption.Create, () =>
                    grains.GetGrain<ITransactionalProductActor>(stat.GrainId).Reset());
                tasks.Add(t);
            }
        }

        // get sellers and orders actors to reset
        // cannot get orders and sellers from shipments
        // because some of them may have been already removed from memory
        foreach (var stat in stats)
        {
            this.logger.LogDebug("{stat}",stat.ToString());
            if (stat.GrainType.SequenceEqual("OrleansApp.Transactional.TransactionalOrderActor,OrleansApp"))
            {
                int num = stat.ActivationCount;
                for(int i = 1; i <= num; i++)
                {
                    tasks.Add( grains.GetGrain<ITransactionalOrderActor>(i).Reset() );
                }
                this.logger.LogWarning("{0} order states reset", num);
                continue;
            }
            if (stat.GrainType.SequenceEqual("OrleansApp.Grains.OrderActor,OrleansApp"))
            {
                int num = stat.ActivationCount;
                for(int i = 1; i <= num; i++)
                {
                    tasks.Add( grains.GetGrain<IOrderActor>(i).Reset() );
                }
                this.logger.LogWarning("{0} order states reset", num);
                continue;
            }
            if (stat.GrainType.SequenceEqual("OrleansApp.Grains.SellerActor,OrleansApp"))
            {
                int num = stat.ActivationCount;
                for(int i = 1; i <= num; i++)
                {
                    tasks.Add( grains.GetGrain<ISellerActor>(i).Reset() );
                }
                this.logger.LogWarning("{0} seller states reset", num);
                continue;
            }
            if (stat.GrainType.SequenceEqual("OrleansApp.Grains.CustomerActor,OrleansApp"))
            {
                int num = stat.ActivationCount;
                for(int i = 1; i <= num; i++)
                {
                    tasks.Add( grains.GetGrain<ICustomerActor>(i).Reset() );
                }
                this.logger.LogWarning("{0} customer states reset", num);
            }
            // seal carts that have not checked out in past run
            if (stat.GrainType.SequenceEqual("OrleansApp.Grains.CartActor,OrleansApp"))
            {
                int num = stat.ActivationCount;
                for(int i = 1; i <= num; i++)
                {
                    tasks.Add( grains.GetGrain<ICartActor>(i).Seal() );
                }
                this.logger.LogWarning("{0} cart states reset", num);
            }
            if (stat.GrainType.SequenceEqual("OrleansApp.Transactional.TransactionalPaymentActor,OrleansApp"))
            {
                int num = stat.ActivationCount;
                for(int i = 1; i <= num; i++)
                {
                    Task t = this.transactionClient.RunTransaction(TransactionOption.Create, () =>
                        grains.GetGrain<ITransactionalPaymentActor>(i).Reset());
                    tasks.Add(t);
                }
                this.logger.LogWarning("{0} transactional payment states reset", num);
            }
            if (stat.GrainType.SequenceEqual("OrleansApp.Grains.PaymentActor,OrleansApp"))
            {
                int num = stat.ActivationCount;
                for(int i = 1; i <= num; i++)
                {
                    tasks.Add( grains.GetGrain<IPaymentActor>(i).Reset() );
                }
                this.logger.LogWarning("{0} payment states reset", num);
            }
            if (stat.GrainType.SequenceEqual("OrleansApp.Transactional.TransactionalStockActor,OrleansApp"))
            {
                int num = stat.ActivationCount;
                this.logger.LogWarning("{0} transactional stock states reset", num);
            }
            if (stat.GrainType.SequenceEqual("OrleansApp.Grains.StockActor,OrleansApp"))
            {
                int num = stat.ActivationCount;
                int j = 1;
                for(int i = 1; i <= num; i++)
                {
                    tasks.Add( grains.GetGrain<IStockActor>(i,j.ToString()).Reset() );
                    j++;
                    if(j == 11) j = 1;
                }
                this.logger.LogWarning("{0} stock states reset", num);
            }
            if (stat.GrainType.SequenceEqual("OrleansApp.Transactional.TransactionalProductActor,OrleansApp"))
            {
                int num = stat.ActivationCount;
                this.logger.LogWarning("{0} transactional product states reset", num);
            }
            if (stat.GrainType.SequenceEqual("OrleansApp.Grains.ProductActor,OrleansApp"))
            {
                int num = stat.ActivationCount;
                int j = 1;
                for(int i = 1; i <= num; i++)
                {
                    tasks.Add( grains.GetGrain<IProductActor>(i,j.ToString()).Reset() );
                    j++;
                    if(j == 11) j = 1;
                }
                this.logger.LogWarning("{0} product states reset", num);
            }
        }
        await this.shipmentService.ResetShipmentActors();

        if (this.config.LogRecords)
        {
            await this.persistence.CleanLog();
        }

        await Task.WhenAll(tasks);
        
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
        if (this.config.LogRecords)
        {
            await persistence.CleanLog();
        }
        if (this.config.AdoNetGrainStorage)
        {
            await persistence.TruncateStorage();
        }
        if (this.config.SellerViewPostgres)
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