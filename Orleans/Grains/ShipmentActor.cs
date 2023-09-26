using Common.Entities;
using Common.Events;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Orleans.Interfaces;
using Orleans.Runtime;
using Orleans.Infra;
using System.Text;

namespace Orleans.Grains;

public class ShipmentActor : Grain, IShipmentActor
{
    
    private int partitionId;
    private static readonly string Name = typeof(ShipmentActor).FullName;
    private readonly IPersistentState<Dictionary<string,Shipment>> shipments;
    private readonly IPersistentState<Dictionary<string,List<Package>>> packages;   // key: customer ID + "-" + order ID

    private record SellerEntry(DateTime request_date, int sellerId, int customerId, int orderId);

    private readonly Dictionary<int, List<SellerEntry>> sellerInfo;    // key: seller ID, value: info of the oldest un-completed order

    private readonly ILogger<ShipmentActor> _logger;
    private readonly IPersistence _persistence;

    private class ShipmentState
    {
        public Shipment shipment { get; set; }
        public List<Package> packages { get; set; }
        public ShipmentState(){ }
    }

    public ShipmentActor(
        ILogger<ShipmentActor> _logger,
        IPersistence _persistence,
        [PersistentState(stateName: "shipments", storageName: Constants.OrleansStorage)] IPersistentState<Dictionary<string,Shipment>> shipments,
         [PersistentState(stateName: "packages", storageName: Constants.OrleansStorage)] IPersistentState<Dictionary<string,List<Package>>> packages)
	{
        this._logger = _logger;
        this._persistence = _persistence;
        this.shipments = shipments;
        this.packages = packages;
        this.sellerInfo = new Dictionary<int, List<SellerEntry>>();
    }

    public override Task OnActivateAsync(CancellationToken token)
    {
        this.partitionId = (int) this.GetPrimaryKeyLong();
        // persistence
        if(this.shipments.State is null) this.shipments.State = new();
        if(this.packages.State is null) this.packages.State = new();
        // TODO rebuild seller cache from state so after a crash it is rebuilt correctly
        return Task.CompletedTask;
    }

    public Task Reset()
    {
        this.shipments.State.Clear();
        // this.shipments.ClearStateAsync();
        this.packages.State.Clear();
        // this.packages.ClearStateAsync();
        this.sellerInfo.Clear();
        return Task.CompletedTask;
    }

    public async Task ProcessShipment(PaymentConfirmed paymentConfirmed)
    {
        int package_id = 1;

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

        var id = new StringBuilder(paymentConfirmed.customer.CustomerId.ToString()).Append('-').Append(shipment.order_id).ToString();
        try{
            shipments.State.Add(id, shipment);
            packages.State.Add(id, new List<Package>());
        }
        catch(Exception)
        {
            _logger.LogWarning("Shipment {0} processing customer ID {1}. Unique ID {2}", this.partitionId, paymentConfirmed.customer.CustomerId, id);
            return;
        }

        var sellerSet = paymentConfirmed.items.Select(x => x.seller_id).ToHashSet();

        foreach(var sellerId in sellerSet)
        {
            if (!sellerInfo.ContainsKey(sellerId)) {
                sellerInfo.Add(sellerId, new List<SellerEntry>());
                sellerInfo[sellerId].Add(new(now, sellerId, paymentConfirmed.customer.CustomerId, paymentConfirmed.orderId));
                continue;
            }
            var index = 0;
            while (index < sellerInfo[sellerId].Count && sellerInfo[sellerId][index].request_date < now) index++;
            sellerInfo[sellerId].Insert(index, new(now, sellerId, paymentConfirmed.customer.CustomerId, paymentConfirmed.orderId));
        }

        foreach (var item in paymentConfirmed.items)
        {
            Package package = new()
            {
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

            packages.State[id].Add(package);

            package_id++;
        }
        await Task.WhenAll(this.shipments.WriteStateAsync(), this.packages.WriteStateAsync());

        ShipmentNotification shipmentNotification = new ShipmentNotification(paymentConfirmed.customer.CustomerId, paymentConfirmed.orderId, now, paymentConfirmed.instanceId, ShipmentStatus.approved);
        // inform seller
        var tasks = new List<Task>();
        var sellers = paymentConfirmed.items.Select(x => x.seller_id).ToHashSet();
        foreach (var sellerID in sellers)
        {
            var sellerActor = GrainFactory.GetGrain<ISellerActor>(sellerID);
            tasks.Add(sellerActor.ProcessShipmentNotification(shipmentNotification));
        }
        await Task.WhenAll(tasks);

        var orderActor = GrainFactory.GetGrain<IOrderActor>(paymentConfirmed.customer.CustomerId);
        await orderActor.ProcessShipmentNotification(shipmentNotification);
    }

    private Dictionary<int,DateTime> GetOldestOpenShipmentPerSeller()
    {
        return sellerInfo.SelectMany(x=> x.Value ).GroupBy(x=>x.sellerId)
              .Select(g => new { key = g.Key, Sort = g.Min(x => x.request_date) }).Take(10)
              .ToDictionary(g => g.key, g => g.Sort);
    }

    public async Task UpdateShipment(int tid)
    {
        List<Task> tasks = new();
        // impossibility of ensuring one order per seller in this transaction
        // since sellers' packages are distributed across many
        // shipment actors

        var now = DateTime.UtcNow;
        // https://stackoverflow.com/questions/5231845/c-sharp-linq-group-by-on-multiple-columns

        // get oldest 10 orders by seller
        var oldestShipments = GetOldestOpenShipmentPerSeller();

        foreach (var x in oldestShipments)
        {
            var info = sellerInfo[x.Key].First();
            sellerInfo[x.Key].RemoveAt(0);

            var id = new StringBuilder(info.customerId.ToString()).Append('-').Append(info.orderId).ToString();
            List<Package> packages_;
            Shipment shipment;
            try{
                packages_ = this.packages.State[id].Where( p=>p.seller_id == x.Key ).ToList();
                shipment = this.shipments.State[id];
            }
            catch (Exception)
            {
                _logger.LogWarning("Error caught by shipment ator {0}. ID does not exist: {1} - {2}", this.partitionId, id, info);
                continue;
            }
            
            int countDelivered = this.packages.State[id].Where( p=>p.status == PackageStatus.delivered ).Count();

            foreach (var package in packages_)
            {
                package.status = PackageStatus.delivered;
                package.delivery_date = now;
                var deliveryNotification = new DeliveryNotification(
                    shipment.customer_id, package.order_id, package.package_id, package.seller_id,
                    package.product_id, package.product_name, PackageStatus.delivered, now, tid);

                tasks.Add( GrainFactory.GetGrain<ICustomerActor>(info.customerId)
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
                tasks.Add( GrainFactory.GetGrain<ISellerActor>(packages_[0].seller_id)
                    .ProcessShipmentNotification(shipmentNotification) );
                tasks.Add( GrainFactory.GetGrain<IOrderActor>(shipment.customer_id)
                    .ProcessShipmentNotification(shipmentNotification) );

                // log shipment and packages
                var str = JsonSerializer.Serialize(new ShipmentState{ shipment = shipment, packages = packages_ } );
                var sb = new StringBuilder(shipment.customer_id.ToString()).Append('-').Append(shipment.order_id).ToString();
                tasks.Add( _persistence.Log(Name, sb.ToString(), str) );

                this.shipments.State.Remove(id);
                this.packages.State.Remove(id);

            }

        }

        // no need to wait for oneway events
        tasks.Add(this.shipments.WriteStateAsync());
        tasks.Add(this.packages.WriteStateAsync());
        await Task.WhenAll( tasks );
    }

    public Task<List<Shipment>> GetShipments(int customerId)
    {
        return Task.FromResult(shipments.State.Select(x => x.Value).Where(x => x.customer_id == customerId).ToList());
    }
}

