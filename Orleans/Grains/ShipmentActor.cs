using Common.Entities;
using Common.Events;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Orleans.Interfaces;
using Orleans.Runtime;
using RocksDbSharp;
using Orleans.Infra;

namespace Orleans.Grains;

public class ShipmentActor : Grain, IShipmentActor
{
    private int partitionId;
    private readonly IPersistentState<Dictionary<int,Shipment>> shipments;
    private readonly IPersistentState<Dictionary<int,List<Package>>> packages;

    private readonly ILogger<ShipmentActor> _logger;
    private RocksDb db;

    public ShipmentActor(
        ILogger<ShipmentActor> _logger,
        [PersistentState(stateName: "shipments", storageName: Infra.Constants.OrleansStorage)] IPersistentState<Dictionary<int,Shipment>> shipments,
         [PersistentState(stateName: "packages", storageName: Infra.Constants.OrleansStorage)] IPersistentState<Dictionary<int,List<Package>>> packages)
	{
        this._logger = _logger;
        this.shipments = shipments;
        this.packages = packages;
    }

    public override async Task OnActivateAsync(CancellationToken token)
    {
        this.partitionId = (int) this.GetPrimaryKeyLong();
        // persistence
        if(this.shipments.State is null) this.shipments.State = new();
        if(this.packages.State is null) this.packages.State = new();
        this.db = RocksDb.Open(Constants.rocksDBOption, typeof(ShipmentActor).FullName);

        await base.OnActivateAsync(token);
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

        // in memory
        shipments.State.Add(shipment.order_id, shipment);
        packages.State.Add(shipment.order_id, new List<Package>());

        foreach (var item in paymentConfirmed.items)
        {
            Package package = new()
            {
                order_id = paymentConfirmed.orderId,
                package_id = package_id,
                status = PackageStatus.shipped,
                freight_value = item.freight_value,
                shipping_date = now,
                seller_id = item.seller_id,
                product_id = item.product_id,
                product_name = item.product_name,
                quantity = item.quantity
            };

            packages.State[shipment.order_id].Add(package);
           
            package_id++;
        }
        await Task.WhenAll(this.shipments.WriteStateAsync(), this.packages.WriteStateAsync());

        ShipmentNotification shipmentNotification = new ShipmentNotification(paymentConfirmed.customer.CustomerId, paymentConfirmed.orderId, now, paymentConfirmed.instanceId);
        // inform seller
        var tasks = new List<Task>();
        var sellers = paymentConfirmed.items.Select(x => x.seller_id).ToHashSet();
        foreach (var sellerID in sellers)
        {
            var sellerActor = GrainFactory.GetGrain<ISellerActor>(sellerID);
            tasks.Add(sellerActor.ProcessShipmentNotification(shipmentNotification));
        }
        await Task.WhenAll(tasks);

        var orderActor = GrainFactory.GetGrain<IOrderActor>(paymentConfirmed.orderId);
        await orderActor.ProcessShipmentNotification(shipmentNotification);
    }

    public async Task UpdateShipment(int tid)
    {
        // impossibility of ensuring one order per seller in this transaction
        // since sellers' packages are distributed across many
        // shipment actors

        var now = DateTime.UtcNow;

        var q = this.packages.State.SelectMany(x=>x.Value)
            .GroupBy(x => x.seller_id)
                            .Select(g => new { sellerId = g.Key, orderId = g.Min(x => x.order_id) }).Take(10);

        foreach(var x in q)
        {
            var packages_ = this.packages.State[x.orderId].Where( p=>p.seller_id == x.sellerId ).ToList();
            var shipment = this.shipments.State[x.orderId];

            List<Task> tasks = new(packages_.Count() + 1);
            int countDelivered = this.packages.State[x.orderId].Where( p=>p.status == PackageStatus.delivered ).Count();

            foreach (var package in packages_)
            {
                package.status = PackageStatus.delivered;
                package.delivery_date = now;
                var deliveryNotification = new DeliveryNotification(
                    shipment.customer_id, package.order_id, package.package_id, package.seller_id,
                    package.product_id, package.product_name, PackageStatus.delivered, now, tid);

                tasks.Add( GrainFactory.GetGrain<ICustomerActor>(shipment.customer_id)
                    .NotifyDelivery(deliveryNotification) );
                tasks.Add( GrainFactory.GetGrain<ISellerActor>(package.seller_id)
                    .ProcessDeliveryNotification(deliveryNotification) );
            }

            if (shipment.status == ShipmentStatus.approved)
            {
                shipment.status = ShipmentStatus.delivery_in_progress;
                ShipmentNotification shipmentNotification = new ShipmentNotification(
                        shipment.customer_id, shipment.order_id, now, tid, ShipmentStatus.delivery_in_progress);
                tasks.Add( GrainFactory.GetGrain<IOrderActor>(shipment.order_id)
                    .ProcessShipmentNotification(shipmentNotification) );
            }

            if (shipment.package_count == countDelivered + packages_.Count())
            {
                shipment.status = ShipmentStatus.concluded;
                ShipmentNotification shipmentNotification = new ShipmentNotification(
                shipment.customer_id, shipment.order_id, now, tid, ShipmentStatus.concluded);
                tasks.Add(     GrainFactory.GetGrain<ISellerActor>(packages_[0].seller_id)
                    .ProcessShipmentNotification(shipmentNotification));
                tasks.Add (    GrainFactory.GetGrain<IOrderActor>(shipment.order_id)
                    .ProcessShipmentNotification(shipmentNotification)          
                    );

                // log shipment and packages
                var str = JsonSerializer.Serialize((shipment, packages_));
                db.Put(shipment.customer_id.ToString() + "-" + shipment.order_id.ToString(), str);
                _logger.LogWarning($"Log shipment info to RocksDB. ");

                this.shipments.State.Remove(x.orderId);
                this.packages.State.Remove(x.orderId);
            }

            // no need to wait for oneway events
            await Task.WhenAll( tasks );

        }

        await Task.WhenAll( this.shipments.WriteStateAsync(), this.packages.WriteStateAsync() );

    }
}


