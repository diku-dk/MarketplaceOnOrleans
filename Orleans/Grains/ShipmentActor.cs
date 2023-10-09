using Common.Entities;
using Common.Events;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using Orleans.Infra;
using Common;
using Orleans.Concurrency;
using Orleans.Abstract;
using Orleans.Interfaces;
using Orleans.Transactional;

namespace Orleans.Grains;

[Reentrant]
public sealed class ShipmentActor : AbstractShipmentActor
{

    private readonly IPersistentState<SortedDictionary<int,Shipment>> shipments;
    private readonly IPersistentState<SortedDictionary<int,List<Package>>> packages;   // key: customer ID + "-" + order ID
    private readonly IPersistentState<NextShipmentIdState> nextShipmentId;


    public ShipmentActor(
         [PersistentState(stateName: "shipments", storageName: Constants.OrleansStorage)] IPersistentState<SortedDictionary<int,Shipment>> shipments,
         [PersistentState(stateName: "packages", storageName: Constants.OrleansStorage)] IPersistentState<SortedDictionary<int,List<Package>>> packages,
         [PersistentState(stateName: "nextShipmentId", storageName: Constants.OrleansStorage)] IPersistentState<NextShipmentIdState> nextShipmentId,
         IPersistence persistence,
         AppConfig options,
         ILogger<ShipmentActor> logger) : base(persistence, options, logger)
	{
        this.shipments = shipments;
        this.packages = packages;
        this.nextShipmentId = nextShipmentId;
    }

    public override async Task OnActivateAsync(CancellationToken token)
    {
        await base.OnActivateAsync(token);
    }

    public override async Task Reset()
    {
        this.shipments.State.Clear();
        this.packages.State.Clear();
        if (this.config.OrleansStorage)
        {
            await Task.WhenAll(this.shipments.WriteStateAsync(), this.packages.WriteStateAsync());
        }
    }

    protected override Dictionary<int,int> GetOldestOpenShipmentPerSeller()
    {
        return this.packages.State.Take(10).SelectMany(x=> x.Value).GroupBy(x=>x.seller_id).Select(g => new { key = g.Key, Sort = g.Min(x => x.shipment_id) }).ToDictionary(g => g.key, g => g.Sort);
    }

    public override Task<List<Shipment>> GetShipments(int customerId)
    {
        return Task.FromResult(this.shipments.State.Select(x => x.Value).Where(x => x.customer_id == customerId).ToList());
    }

    protected override Task<(Shipment, List<Package>)> GetShipmentById(int id)
    {
        try
        {
            var packages = this.packages.State[id].ToList();
            var shipment = this.shipments.State[id];
            return Task.FromResult((shipment, packages));
        }
        catch (Exception)
        {
            var str = string.Format("Error caught by shipment ator {0}. ID does not exist: {1}", this.partitionId, id);
            this.logger.LogWarning(str);
            throw new InvalidOperationException(str);
        }
    }

    public override async Task InsertShipmentIntoState(int id, Shipment shipment, List<Package> packages)
    {
        try
        {
            this.shipments.State.Add(id, shipment);
            this.packages.State.Add(id, packages);
        }
        catch (Exception e)
        {
            var str = string.Format("Shipment {0} processing customer ID {1} with unique ID {2}. Error: {3}", this.partitionId, shipment.customer_id, id, e.Message);
            this.logger.LogError(str);
            throw new InvalidOperationException(str);
        }
        if (this.config.OrleansStorage)
            await Task.WhenAll(this.shipments.WriteStateAsync(), this.packages.WriteStateAsync(), this.nextShipmentId.WriteStateAsync());
    }

    protected override async Task DeleteShipmentById(int id)
    {
        this.shipments.State.Remove(id);
        this.packages.State.Remove(id);
        // no need to wait for oneway events
        if (this.config.OrleansStorage)
        {
            await Task.WhenAll(this.shipments.WriteStateAsync(), this.packages.WriteStateAsync());
        }
    }

    public override Task<int> GetNextShipmentId()
    {
        return Task.FromResult(nextShipmentId.State.GetNextShipmentId().Value);
    }

    protected override void SetPackageToDelivered(int id, Package package, DateTime time)
    {
        package.status = PackageStatus.delivered;
        package.delivery_date = time;
    }

    protected override void UpdateShipmentStatus(int id, ShipmentStatus status)
    {
        this.shipments.State[id].status = status;
    }

    public override IOrderActor GetOrderActor(int customerId)
    {
        return this.GrainFactory.GetGrain<IOrderActor>(customerId);
    }

}

