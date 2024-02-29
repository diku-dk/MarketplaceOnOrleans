using Common.Config;
using Common.Entities;
using Common.Events;
using Microsoft.Extensions.Logging;
using Orleans.Interfaces.SellerView;
using OrleansApp.Grains;
using OrleansApp.Infra;
using OrleansApp.Interfaces;
using System.Text;
using System.Text.Json;

namespace OrleansApp.Abstract;

public abstract class AbstractShipmentActor : Grain, IShipmentActor
{
    protected readonly AppConfig config;
    protected int partitionId;

    protected static readonly string Name = typeof(ShipmentActor).FullName;

    protected readonly ILogger<IShipmentActor> logger;
    protected readonly IAuditLogger persistence;

    private delegate ISellerActor GetSellerActorDelegate(int sellerId);
    private readonly GetSellerActorDelegate getSellerDelegate;

    private ISellerActor GetSellerActor(int sellerId)
    {
        return this.GrainFactory.GetGrain<ISellerActor>(sellerId);
    }

    private ISellerViewActor GetSellerViewActor(int sellerId)
    {
        return this.GrainFactory.GetGrain<ISellerViewActor>(sellerId);
    }

    public class NextShipmentIdState
    {
        public int Value { get; set; }
        public NextShipmentIdState() { this.Value = 0; }
        public NextShipmentIdState(int value) { this.Value = value; }
        public NextShipmentIdState GetNextShipmentId()
        {
            this.Value++;
            return this;
        }
    }

    public class ShipmentState
    {
        public Shipment shipment { get; set; }
        public List<Package> packages { get; set; }
        public ShipmentState() { }
    }

    public AbstractShipmentActor(IAuditLogger persistence,
                                 AppConfig options,
                                 ILogger<IShipmentActor> logger)
    {
        this.persistence = persistence;
        this.config = options;
        this.logger = logger;
        this.getSellerDelegate = config.SellerViewPostgres ? GetSellerViewActor : GetSellerActor;
    }

    public override Task OnActivateAsync(CancellationToken token)
    {
        this.partitionId = (int)this.GetPrimaryKeyLong();
        return Task.CompletedTask;
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

        var id = await GetNextShipmentId();
        var packages = new List<Package>();

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
        await InsertShipmentIntoState(id, shipment, packages);
    
        ShipmentNotification shipmentNotification = new ShipmentNotification(paymentConfirmed.customer.CustomerId, paymentConfirmed.orderId, now, paymentConfirmed.instanceId, ShipmentStatus.approved);
        // inform seller
        var tasks = new List<Task>();
        var sellers = paymentConfirmed.items.Select(x => x.seller_id).ToHashSet();
        foreach (var sellerId in sellers)
        {
            var sellerActor = this.getSellerDelegate(sellerId);
            tasks.Add(sellerActor.ProcessShipmentNotification(shipmentNotification));
        }
        
        var orderActor = this.GetOrderActor(paymentConfirmed.customer.CustomerId);
        tasks.Add(orderActor.ProcessShipmentNotification(shipmentNotification));
        await Task.WhenAll(tasks);
    }

    /**
     *  Impossibility of ensuring one order per seller in this transaction
     *  (without coordination among all shipments!)
     *  since sellers' packages are distributed across many shipment actors
     */
    public async Task UpdateShipment(string tid)
    {
        // https://stackoverflow.com/questions/5231845/c-sharp-linq-group-by-on-multiple-columns

        // get oldest 10 orders by seller
        var oldestShipments = config.OrleansTransactions ? await GetOldestOpenShipmentPerSellerAsync() : GetOldestOpenShipmentPerSeller();

        await DoUpdateShipments(tid, oldestShipments);
    }

    protected async Task DoUpdateShipments(string tid, Dictionary<int, int> oldestShipments)
    {
        var now = DateTime.UtcNow;
        List<Task> tasks = new();
        foreach (var info in oldestShipments)
        {
            var res = await this.GetShipmentById(info.Value);
            List<Package> packages = res.Item2;
            Shipment shipment = res.Item1;

            var sellerPackages = packages.Where(p => p.seller_id == info.Key).ToList();
            int countDelivered = packages.Where(p => p.status == PackageStatus.delivered).Count();

            foreach (var package in sellerPackages)
            {
                this.SetPackageToDelivered(info.Value, package, now);

                var deliveryNotification = new DeliveryNotification(
                    shipment.customer_id, package.order_id, package.package_id, package.seller_id,
                    package.product_id, package.product_name, PackageStatus.delivered, now, tid);

                tasks.Add(GrainFactory.GetGrain<ICustomerActor>(package.customer_id)
                     .NotifyDelivery(deliveryNotification));
                tasks.Add(this.getSellerDelegate(package.seller_id).ProcessDeliveryNotification(deliveryNotification));
            }

            if (shipment.status == ShipmentStatus.approved)
            {
                this.UpdateShipmentStatus(info.Value, ShipmentStatus.delivery_in_progress);

                ShipmentNotification shipmentNotification = new ShipmentNotification(
                        shipment.customer_id, shipment.order_id, now, tid, ShipmentStatus.delivery_in_progress);
                tasks.Add(this.GetOrderActor(shipment.customer_id).ProcessShipmentNotification(shipmentNotification));
            }

            if (shipment.package_count == countDelivered + sellerPackages.Count)
            {
                this.UpdateShipmentStatus(info.Value, ShipmentStatus.concluded);
                ShipmentNotification shipmentNotification = new ShipmentNotification(
                    shipment.customer_id, shipment.order_id, now, tid, ShipmentStatus.concluded);

                // should notify all sellers included in the shipment
                var sellerIDs = sellerPackages.Select(p => p.seller_id).Distinct();
                foreach(var sellerID in sellerIDs)
                {
                    tasks.Add(this.getSellerDelegate(sellerID).ProcessShipmentNotification(shipmentNotification));
                }

                tasks.Add(GetOrderActor(shipment.customer_id).ProcessShipmentNotification(shipmentNotification));

                // log shipment and packages
                if (this.config.LogRecords)
                {
                    var str = JsonSerializer.Serialize(new ShipmentState { shipment = shipment, packages = packages });
                    var sb = new StringBuilder(shipment.customer_id.ToString()).Append('-').Append(shipment.order_id).ToString();
                    tasks.Add(persistence.Log(Name, sb.ToString(), str));
                }

                tasks.Add(this.DeleteShipmentById(info.Value));

            }

        }

        await Task.WhenAll(tasks);
    }

    public abstract IOrderActor GetOrderActor(int customerId);

    protected abstract void UpdateShipmentStatus(int id, ShipmentStatus status);

    protected abstract void SetPackageToDelivered(int id, Package package, DateTime time);

    public abstract Task Reset();

    public abstract Task InsertShipmentIntoState(int id, Shipment shipment, List<Package> packages);

    public abstract Task<int> GetNextShipmentId();

    protected virtual Dictionary<int, int> GetOldestOpenShipmentPerSeller()
    {
        throw new NotImplementedException();
    }

    protected virtual Task<Dictionary<int, int>> GetOldestOpenShipmentPerSellerAsync()
    {
        throw new NotImplementedException();
    }

    protected abstract Task<(Shipment, List<Package>)> GetShipmentById(int id);

    protected abstract Task DeleteShipmentById(int id);

    public abstract Task<List<Shipment>> GetShipments(int customerId);

    public virtual Task UpdateShipment(string tid, ISet<(int customerId, int orderId, int sellerId)> entries)
    {
        throw new NotImplementedException();
    }

}