using Common.Config;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans.Infra.SellerDb;
using OrleansApp.Infra;
using OrleansApp.Interfaces;
using OrleansApp.Transactional;

namespace OrleansApp.Service;

public sealed class CustomShipmentServiceImpl : IShipmentService
{
    private delegate IShipmentActor GetShipmentActorDelegate(int partitionId);

    private readonly AppConfig config;
    private readonly IDbContextFactory<SellerDbContext> dbContextFactory;
    private readonly GetShipmentActorDelegate callback;
    private readonly IGrainFactory grainFactory;
    private readonly ILogger<CustomShipmentServiceImpl> logger;

    private const string sqlGetItemsForUpdate = "SELECT * FROM public.order_entries oe LIMIT 10 FOR UPDATE SKIP LOCKED";

    public CustomShipmentServiceImpl(AppConfig config, IDbContextFactory<SellerDbContext> dbContextFactory, IGrainFactory grainFactory, ILogger<CustomShipmentServiceImpl> logger)
    {
        this.config = config;
        this.dbContextFactory = dbContextFactory;
        this.callback = config.OrleansTransactions ? GetTransactionalShipmentActor : GetShipmentActor;
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
            foreach ( var oe in orderEntries)
            {
                int id = Helper.GetShipmentActorID(oe.customer_id, config.NumShipmentActors);
                if(!dict.ContainsKey(id))
                {
                    dict.Add(id, new());
                }
                dict[id].Add((oe.customer_id, oe.order_id, oe.seller_id));
            } 
        }
        // end transaction otherwise there is the risk of conflict with updates made by seller view actors

        if(dict.Count == 0)
        {
            throw new ApplicationException("No order entries were retrieved from the database.");
        }

        // FIXME some requests can obtain the same entries though... the abstractshipmentactor must avoid the cases where the shipment is not found
        List<Task> tasks = new List<Task>(dict.Count);
        foreach (var entry in dict)
        {
            var grain = this.callback(entry.Key);
            tasks.Add(grain.UpdateShipment(instanceId, entry.Value));
        }
        await Task.WhenAll(tasks);
    }

    private IShipmentActor GetShipmentActor(int partitionId)
    {
        return this.grainFactory.GetGrain<IShipmentActor>(partitionId);
    }

    private ITransactionalShipmentActor GetTransactionalShipmentActor(int partitionId)
    {
        return this.grainFactory.GetGrain<ITransactionalShipmentActor>(partitionId);
    }

}

