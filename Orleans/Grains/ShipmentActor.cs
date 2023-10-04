using Common.Entities;
using Common.Events;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Orleans.Interfaces;
using Orleans.Runtime;
using Orleans.Infra;
using System.Text;
using Common;
using Microsoft.Extensions.Options;
using Orleans.Concurrency;

namespace Orleans.Grains;

[Reentrant]
public class ShipmentActor : Grain, IShipmentActor
{
    private readonly AppConfig config;
    private int partitionId;
    
    private static readonly string Name = typeof(ShipmentActor).FullName;

    private readonly IPersistentState<SortedDictionary<int,Shipment>> shipments;
    private readonly IPersistentState<SortedDictionary<int,List<Package>>> packages;   // key: customer ID + "-" + order ID
    private readonly IPersistentState<NextShipmentIdState> nextShipmentId;

    private readonly ILogger<ShipmentActor> logger;
    private readonly IPersistence persistence;

    public class NextShipmentIdState
    {
        public int Value { get; set; }
        public NextShipmentIdState(){ this.Value = 0; }
        public NextShipmentIdState(int value){ this.Value = value; }
        public NextShipmentIdState GetNextShipmentId()
        {
            this.Value++;
            return this;
        }
    }

    private class ShipmentState
    {
        public Shipment shipment { get; set; }
        public List<Package> packages { get; set; }
        public ShipmentState(){ }
    }

    public ShipmentActor(
         [PersistentState(stateName: "shipments", storageName: Constants.OrleansStorage)] IPersistentState<SortedDictionary<int,Shipment>> shipments,
         [PersistentState(stateName: "packages", storageName: Constants.OrleansStorage)] IPersistentState<SortedDictionary<int,List<Package>>> packages,
         [PersistentState(stateName: "nextShipmentId", storageName: Constants.OrleansStorage)] IPersistentState<NextShipmentIdState> nextShipmentId,
         IPersistence persistence,
         IOptions<AppConfig> options,
         ILogger<ShipmentActor> logger)
	{
        this.shipments = shipments;
        this.packages = packages;
        this.nextShipmentId = nextShipmentId;
        this.persistence = persistence;
        this.config = options.Value;
        this.logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken token)
    {
        this.partitionId = (int) this.GetPrimaryKeyLong();
        // persistence
        if(this.shipments.State is null) this.shipments.State = new();
        if(this.packages.State is null) this.packages.State = new();
        
        return Task.CompletedTask;
    }

    public async Task Reset()
    {
        this.shipments.State.Clear();
        this.packages.State.Clear();
        if (this.config.OrleansStorage)
        {
            await Task.WhenAll(this.shipments.WriteStateAsync(), this.packages.WriteStateAsync());
        }
    }

    public async Task ProcessShipment(PaymentConfirmed paymentConfirmed)
    {
        DateTime now = DateTime.UtcNow;

        Shipment shipment = new()
        {
            order_id = paymentConfirmed.orderId,
            customer_id = paymentConfirmed.customer.CustomerId,
            package_count = paymentConfirmed.items.Count,
            total_freight_value = paymentConfirmed.items.Sum(i => i.freight_value),
            request_date = now,
            status = ShipmentStatus.approved,
            first_name = paymentConfirmed.customer.FirstName,
            last_name = paymentConfirmed.customer.LastName,
            street = paymentConfirmed.customer.Street,
            complement = paymentConfirmed.customer.Complement,
            zip_code = paymentConfirmed.customer.ZipCode,
            city = paymentConfirmed.customer.City,
            state = paymentConfirmed.customer.State
        };

        var id = nextShipmentId.State.GetNextShipmentId().Value;
        var packages = new List<Package>();
        try{
            this.shipments.State.Add(id, shipment);
            this.packages.State.Add(id, packages);
        }
        catch(Exception)
        {
            this.logger.LogWarning("Shipment {0} processing customer ID {1}. Unique ID {2}", this.partitionId, paymentConfirmed.customer.CustomerId, id);
            return;
        }

        int package_id = 1;
        foreach (var item in paymentConfirmed.items)
        {
            Package package = new()
            {
                shipment_id = id,
                order_id = paymentConfirmed.orderId,
                customer_id = shipment.customer_id,
                package_id = package_id,
                status = PackageStatus.shipped,
                freight_value = item.freight_value,
                shipping_date = now,
                seller_id = item.seller_id,
                product_id = item.product_id,
                product_name = item.product_name,
                quantity = item.quantity
            };

            packages.Add(package);

            package_id++;
        }

        if (this.config.OrleansStorage)
            await Task.WhenAll(this.shipments.WriteStateAsync(), this.packages.WriteStateAsync(), this.nextShipmentId.WriteStateAsync());

        ShipmentNotification shipmentNotification = new ShipmentNotification(paymentConfirmed.customer.CustomerId, paymentConfirmed.orderId, now, paymentConfirmed.instanceId, ShipmentStatus.approved);
        // inform seller
        var tasks = new List<Task>();
        var sellers = paymentConfirmed.items.Select(x => x.seller_id).ToHashSet();
        foreach (var sellerID in sellers)
        {
            var sellerActor = GrainFactory.GetGrain<ISellerActor>(sellerID);
            tasks.Add(sellerActor.ProcessShipmentNotification(shipmentNotification));
        }
        
        var orderActor = GrainFactory.GetGrain<IOrderActor>(paymentConfirmed.customer.CustomerId);
        tasks.Add( orderActor.ProcessShipmentNotification(shipmentNotification) );
        await Task.WhenAll(tasks);
    }

    private Dictionary<int,int> GetOldestOpenShipmentPerSeller()
    {
        return packages.State.Take(10).SelectMany(x=> x.Value).GroupBy(x=>x.seller_id).Select(g => new { key = g.Key, Sort = g.Min(x => x.shipment_id) }).ToDictionary(g => g.key, g => g.Sort);
    }

    public async Task UpdateShipment(string tid)
    {
        List<Task> tasks = new();
        // impossibility of ensuring one order per seller in this transaction
        // since sellers' packages are distributed across many
        // shipment actors

        var now = DateTime.UtcNow;
        // https://stackoverflow.com/questions/5231845/c-sharp-linq-group-by-on-multiple-columns

        // get oldest 10 orders by seller
        var oldestShipments = GetOldestOpenShipmentPerSeller();

        foreach (var info in oldestShipments)
        {
            List<Package> packages_;
            Shipment shipment;
            try{
                packages_ = this.packages.State[info.Value].Where( p=>p.seller_id == info.Key ).ToList();
                shipment = this.shipments.State[info.Value];
            }
            catch (Exception)
            {
                this.logger.LogWarning("Error caught by shipment ator {0}. ID does not exist: {1} - {2}", this.partitionId, info.Value, info);
                continue;
            }
            
            int countDelivered = this.packages.State[info.Value].Where( p=>p.status == PackageStatus.delivered ).Count();

            foreach (var package in packages_)
            {
                package.status = PackageStatus.delivered;
                package.delivery_date = now;
                var deliveryNotification = new DeliveryNotification(
                    shipment.customer_id, package.order_id, package.package_id, package.seller_id,
                    package.product_id, package.product_name, PackageStatus.delivered, now, tid);

                tasks.Add( GrainFactory.GetGrain<ICustomerActor>(package.customer_id)
                    .NotifyDelivery(deliveryNotification) );
                tasks.Add( GrainFactory.GetGrain<ISellerActor>(package.seller_id)
                    .ProcessDeliveryNotification(deliveryNotification) );
            }

            if (shipment.status == ShipmentStatus.approved)
            {
                shipment.status = ShipmentStatus.delivery_in_progress;
                ShipmentNotification shipmentNotification = new ShipmentNotification(
                        shipment.customer_id, shipment.order_id, now, tid, ShipmentStatus.delivery_in_progress);
                tasks.Add( GrainFactory.GetGrain<IOrderActor>(shipment.customer_id)
                    .ProcessShipmentNotification(shipmentNotification) );
            }

            if (shipment.package_count == countDelivered + packages_.Count)
            {
                shipment.status = ShipmentStatus.concluded;
                ShipmentNotification shipmentNotification = new ShipmentNotification(
                shipment.customer_id, shipment.order_id, now, tid, ShipmentStatus.concluded);
                // FIXME should notify all sellers included in the shipment
                tasks.Add( GrainFactory.GetGrain<ISellerActor>(packages_[0].seller_id)
                    .ProcessShipmentNotification(shipmentNotification) );
                tasks.Add( GrainFactory.GetGrain<IOrderActor>(shipment.customer_id)
                    .ProcessShipmentNotification(shipmentNotification) );

                // log shipment and packages
                if (this.config.LogRecords){
                    var str = JsonSerializer.Serialize(new ShipmentState{ shipment = shipment, packages = packages_ } );
                    var sb = new StringBuilder(shipment.customer_id.ToString()).Append('-').Append(shipment.order_id).ToString();
                    tasks.Add( persistence.Log(Name, sb.ToString(), str) );
                }
                this.shipments.State.Remove(info.Value);
                this.packages.State.Remove(info.Value);

            }

        }

        // no need to wait for oneway events
        if (this.config.OrleansStorage){
            tasks.Add(this.shipments.WriteStateAsync());
            tasks.Add(this.packages.WriteStateAsync());
        }
        await Task.WhenAll( tasks );
    }

    public Task<List<Shipment>> GetShipments(int customerId)
    {
        return Task.FromResult(shipments.State.Select(x => x.Value).Where(x => x.customer_id == customerId).ToList());
    }
}

