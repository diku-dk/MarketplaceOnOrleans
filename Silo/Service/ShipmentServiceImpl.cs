using System.Net;
using Common.Config;
using Common.Entities;
using Common.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Orleans.Infra.SellerDb;
using OrleansApp.Infra;
using OrleansApp.Interfaces;
using OrleansApp.Transactional;
namespace Silo.Service;


public sealed class ShipmentServiceImpl : IShipmentService
{
    private delegate IShipmentActor GetShipmentActorDelegate(int partitionId);

    private readonly AppConfig config;
    private readonly IDbContextFactory<SellerDbContext> dbContextFactory;
    private readonly GetShipmentActorDelegate callback;
    private readonly IGrainFactory grainFactory;
    private readonly ILogger<ShipmentServiceImpl> logger;

    // TODO move flag to config
    private readonly bool eager = false;

    private const string sqlGetItemsForUpdate = "SELECT * FROM public.order_entries oe LIMIT 10 FOR UPDATE SKIP LOCKED";

    public ShipmentServiceImpl(AppConfig config, IDbContextFactory<SellerDbContext> dbContextFactory, [FromServices] IGrainFactory grainFactory, ILogger<ShipmentServiceImpl> logger)
    {
        this.config = config;
        this.dbContextFactory = dbContextFactory;
        this.callback = config.OrleansTransactions ? GetTransactionalShipmentActor : GetShipmentActor;
        this.grainFactory = grainFactory;
        this.logger = logger;
    }

    public async Task UpdateShipment(string instanceId)
    {
        if (eager)
        {
            List<Task> tasks = new List<Task>(config.NumShipmentActors);
            for (int i = 0; i < config.NumShipmentActors; i++)
            {
                var grain = this.callback(i);
                tasks.Add(grain.UpdateShipment(instanceId));
            }
            await Task.WhenAll(tasks);
        } else
        {
            // build dictionary of shipment actors (key) and (customerId | orderId | sellerId)
            Dictionary<int, HashSet<(int customerId, int orderId, int sellerId)>> dict = new();

            using var dbContext = dbContextFactory.CreateDbContext();
            using (var tx = dbContext.Database.BeginTransaction())
            {
                var orderEntries = dbContext.OrderEntries.FromSqlRaw(sqlGetItemsForUpdate);
                foreach ( var oe in orderEntries)
                {
                    int id = Helper.GetShipmentActorID(oe.customer_id, config.NumShipmentActors);
                    dict.TryAdd( id , new() );

                    dict[id].Add((oe.customer_id, oe.order_id, oe.seller_id));
                }
                
            }
            // end transaction otherwise there is the risk to conflixt with updates made by seller view actors
            // FIXME some requests will obtain the same entries though... the abstractshipmentactor must avoid the cases where the shipment is not found
            List<Task> tasks = new List<Task>(dict.Count);
            foreach (var entry in dict)
            {
                var grain = this.callback(entry.Key);
                tasks.Add(grain.UpdateShipment(instanceId, entry.Value));
            }
            await Task.WhenAll(tasks);

        }
    
    }

    private IShipmentActor GetShipmentActor(int partitionId)
    {
        return grainFactory.GetGrain<IShipmentActor>(partitionId);
    }

    private ITransactionalShipmentActor GetTransactionalShipmentActor(int partitionId)
    {
        return grainFactory.GetGrain<ITransactionalShipmentActor>(partitionId);
    }

}

