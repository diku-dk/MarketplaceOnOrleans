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
    private readonly IPersistentState<Dictionary<string,Shipment>> shipments;
    private readonly IPersistentState<Dictionary<string,List<Package>>> packages;   // key: customer ID + "-" + order ID

    private record SellerEntry(DateTime request_date, int customerId, int orderId);

    private readonly Dictionary<int, List<SellerEntry>> sellerInfo;    // key: seller ID, value: info of the oldest un-completed order

    private readonly ILogger<ShipmentActor> _logger;
    readonly IPersistence _persistence;

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
                sellerInfo[sellerId].Add(new(now, paymentConfirmed.customer.CustomerId, paymentConfirmed.orderId));
                continue;
            }
            var index = 0;
            while (index < sellerInfo[sellerId].Count && sellerInfo[sellerId][index].request_date < now) index++;
            sellerInfo[sellerId].Insert(index, new(now, paymentConfirmed.customer.CustomerId, paymentConfirmed.orderId));
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

    public async Task UpdateShipment(int tid)
    {
        // impossibility of ensuring one order per seller in this transaction
        // since sellers' packages are distributed across many
        // shipment actors

        var now = DateTime.UtcNow;
        // https://stackoverflow.com/questions/5231845/c-sharp-linq-group-by-on-multiple-columns

        foreach (var x in sellerInfo)
        {
            if(x.Value.Count == 0) continue;
            var info = x.Value.First();
            x.Value.RemoveAt(0);

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
            List<Task> tasks = new(packages_.Count + 1);
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

            if (shipment.package_count == countDelivered + packages_.Count())
            {
                shipment.status = ShipmentStatus.concluded;
                ShipmentNotification shipmentNotification = new ShipmentNotification(
                shipment.customer_id, shipment.order_id, now, tid, ShipmentStatus.concluded);
                tasks.Add(     GrainFactory.GetGrain<ISellerActor>(packages_[0].seller_id)
                    .ProcessShipmentNotification(shipmentNotification));
                tasks.Add (    GrainFactory.GetGrain<IOrderActor>(shipment.customer_id)
                    .ProcessShipmentNotification(shipmentNotification)          
                    );

                // log shipment and packages
                var str = JsonSerializer.Serialize((shipment, packages_));
                _persistence.Put(typeof(ShipmentActor).FullName, shipment.customer_id.ToString() + "-" + shipment.order_id.ToString(), str);

                this.shipments.State.Remove(id);
                this.packages.State.Remove(id);
            }

            // no need to wait for oneway events
            await Task.WhenAll( tasks );

        }

        await Task.WhenAll( this.shipments.WriteStateAsync(), this.packages.WriteStateAsync() );
    }

    public Task<List<Shipment>> GetShipments(int customerId)
    {
        return Task.FromResult(shipments.State.Select(x => x.Value).Where(x => x.customer_id == customerId).ToList());
    }
}

