using Common.Entities;
using Microsoft.Extensions.Logging;
using OrleansApp.Abstract;
using OrleansApp.Infra;
using OrleansApp.Interfaces;
using Orleans.Transactions.Abstractions;
using Common.Config;
using Orleans.Concurrency;

namespace OrleansApp.Transactional;

[Reentrant]
public sealed class TransactionalShipmentActor : AbstractShipmentActor, ITransactionalShipmentActor
{

    private readonly ITransactionalState<SortedDictionary<int, Shipment>> shipments;
    private readonly ITransactionalState<SortedDictionary<int, List<Package>>> packages;
    private readonly ITransactionalState<NextShipmentIdState> nextShipmentId;

    public TransactionalShipmentActor(
         [TransactionalState(stateName: "shipments", storageName: Constants.OrleansStorage)] ITransactionalState<SortedDictionary<int, Shipment>> shipments,
         [TransactionalState(stateName: "packages", storageName: Constants.OrleansStorage)] ITransactionalState<SortedDictionary<int, List<Package>>> packages,
         [TransactionalState(stateName: "nextShipmentId", storageName: Constants.OrleansStorage)] ITransactionalState<NextShipmentIdState> nextShipmentId,
         IAuditLogger persistence, 
         AppConfig options, 
         ILogger<TransactionalShipmentActor> logger) : base(persistence, options, logger)
    {
        this.shipments = shipments;
        this.packages = packages;
        this.nextShipmentId = nextShipmentId;
    }

    public override async Task OnActivateAsync(CancellationToken token)
    {
        await base.OnActivateAsync(token);
    }

    public override async Task<int> GetNextShipmentId()
    {
        return await this.nextShipmentId.PerformUpdate(id => ++id.Value);
    }

    public override async Task<List<Shipment>> GetShipments(int customerId)
    {
        return await this.shipments.PerformRead(s => {
            return s.Select(x => x.Value).Where(x => x.customer_id == customerId).ToList();
        });
    }

    public override Task InsertShipmentIntoState(int id, Shipment shipment, List<Package> packages)
    {
        Task t1 = this.shipments.PerformUpdate(s => { s.Add(id, shipment); });
        Task t2 = this.packages.PerformUpdate(p => { p.Add(id, packages); });
        return Task.WhenAll(t1, t2);
    }

    public override Task Reset()
    {
        Task t1 = this.shipments.PerformUpdate(s => { s.Clear(); });
        Task t2 = this.packages.PerformUpdate(p => { p.Clear(); });
        return Task.WhenAll(t1, t2);
    }

    protected override Task DeleteShipmentById(int id)
    {
        Task t1 = this.shipments.PerformUpdate(s => { s.Remove(id); });
        Task t2 = this.packages.PerformUpdate(p => { p.Remove(id); });
        return Task.WhenAll(t1, t2);
    }

    protected override async Task<(Shipment, List<Package>)> GetShipmentById(int id)
    {
        var s = this.shipments.PerformRead(s => s[id]);
        var p = this.packages.PerformRead(p => p[id]);
        await Task.WhenAll(s, p);
        return (s.Result, p.Result);
    }

    protected override Task<Dictionary<int, int>> GetOldestOpenShipmentPerSellerAsync()
    {
        return this.packages
            .PerformRead(dic=>dic.Take(10).
            SelectMany(x => x.Value).
            GroupBy(x => x.seller_id).
            Select(g => new { key = g.Key, Sort = g.Min(x => x.shipment_id) }).
            ToDictionary(g => g.key, g => g.Sort));
    }

    protected override async void SetPackageToDelivered(int id, Package package, DateTime time)
    {
        await this.packages.PerformUpdate(p => {
            var package_ = p[id].Where(p => p.package_id == package.package_id).First();
            package_.status = PackageStatus.delivered;
            package_.delivery_date = time;
        });
    }

    protected override async void UpdateShipmentStatus(int id, ShipmentStatus status)
    {
        await this.shipments.PerformUpdate(s => {
            s[id].status = status;
        });
    }

    public override IOrderActor GetOrderActor(int customerId)
    {
        return this.GrainFactory.GetGrain<ITransactionalOrderActor>(customerId);
    }
}

