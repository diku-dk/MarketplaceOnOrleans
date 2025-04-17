using Common.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrleansApp.Infra.SellerDb;
using OrleansApp.Infra;
using OrleansApp.Interfaces;
using OrleansApp.Transactional;

namespace OrleansApp.Service;

/**
 * This class represents an attempt to avoid the substantial coordination required when updating
 * shipment/packages. However, the latency incurred on querying postgres refrains achieving the envisioned
 * benefits.
 */
public sealed class CustomShipmentServiceImpl : IShipmentService
{
    private readonly AppConfig config;
    private readonly IDbContextFactory<SellerDbContext> dbContextFactory;
    private readonly IGrainFactory grainFactory;
    private readonly ILogger<CustomShipmentServiceImpl> logger;

    private const string sqlGetItemsForUpdate = "SELECT * FROM public.order_entries oe LIMIT 10 FOR UPDATE SKIP LOCKED";

    public CustomShipmentServiceImpl(AppConfig config, IDbContextFactory<SellerDbContext> dbContextFactory, IGrainFactory grainFactory, ILogger<CustomShipmentServiceImpl> logger)
    {
        this.config = config;
        this.dbContextFactory = dbContextFactory;
        this.grainFactory = grainFactory;
        this.logger = logger;
    }

    public async Task UpdateShipment(string instanceId)
    {
        // build dictionary of shipment actors (key) and (customerId | orderId | sellerId)
        Dictionary<int, HashSet<(int customerId, int orderId, int sellerId)>> dict = new();

        using var dbContext = this.dbContextFactory.CreateDbContext();
        using (var tx = dbContext.Database.BeginTransaction())
        {
            var orderEntries = dbContext.OrderEntries.FromSqlRaw(sqlGetItemsForUpdate);
            foreach (var oe in orderEntries)
            {
                int id = Helper.GetShipmentActorID(oe.customer_id, config.NumShipmentActors);
                if(!dict.TryGetValue(id, out HashSet<(int customerId, int orderId, int sellerId)> value))
                {
                    value = new();
                    dict.Add(id, value);
                }
                value.Add((oe.customer_id, oe.order_id, oe.seller_id));
            } 
        }
        // end transaction otherwise there is the risk of conflict with updates made by seller view actors

        if(dict.Count == 0)
        {
            this.logger.LogWarning("No order entries were retrieved from the database!");
            throw new ApplicationException("No order entries were retrieved from the database!");
        }

        this.logger.LogInformation(dict.Count+ " order entries retrieved from the database.");
        // FIXME some requests can obtain the same entries though... the abstractshipmentactor must avoid the cases where the shipment is not found
        List<Task> tasks = new List<Task>(dict.Count);
        if (this.config.OrleansTransactions)
        {
            foreach (var entry in dict)
            {
                var grain = this.GetTxShipmentActor(entry.Key);
                tasks.Add(grain.UpdateShipment(instanceId, entry.Value));
            }
        } else
        {
            foreach (var entry in dict)
            {
                var grain = this.GetDefaultShipmentActor(entry.Key);
                tasks.Add(grain.UpdateShipment(instanceId, entry.Value));
            }
        }
        
        await Task.WhenAll(tasks);
    }

    // FIXME duplicated. can create an abstract shipment service to avoid it
    public async Task ResetShipmentActors()
    {
        List<Task> tasks = new List<Task>(config.NumShipmentActors);
        for(int i = 0; i < this.config.NumShipmentActors; i++)
        {
            if (this.config.OrleansTransactions)
            {
                var grain = GetTxShipmentActor(i);
                tasks.Add(grain.Reset());
            }
            else {
                var grain = this.GetDefaultShipmentActor(i);
                tasks.Add(grain.Reset());
            }
        }
        await Task.WhenAll(tasks);
        this.logger.LogWarning("{0} shipment states reset", this.config.NumShipmentActors);
    }

    private IShipmentActor GetDefaultShipmentActor(int partitionId)
    {
        return this.grainFactory.GetGrain<IShipmentActor>(partitionId);
    }

    private ITransactionalShipmentActor GetTxShipmentActor(int partitionId)
    {
        return this.grainFactory.GetGrain<ITransactionalShipmentActor>(partitionId);
    }

}

