using Common.Driver;
using Common.Entities;
using Common.Events;
using Orleans.Interfaces;
using Orleans.Runtime;

namespace Orleans.Grains;

public class ShipmentActor : Grain, IShipmentActor
{
    private int partitionId;

    private readonly IPersistentState<Dictionary<int,Shipment>> shipments;
    private readonly IPersistentState<Dictionary<int,List<Package>>> packages;

	public ShipmentActor(
        [PersistentState(stateName: "shipments", storageName: Infra.Constants.OrleansStorage)] IPersistentState<Dictionary<int,Shipment>> shipments,
         [PersistentState(stateName: "packages", storageName: Infra.Constants.OrleansStorage)] IPersistentState<Dictionary<int,List<Package>>> packages)
	{
        this.shipments = shipments;
        this.packages = packages;
	}

    public override Task OnActivateAsync(CancellationToken token)
    {
        this.partitionId = (int) this.GetPrimaryKeyLong();
        if(this.shipments.State is null) this.shipments.State = new();
        if(this.packages.State is null) this.packages.State = new();
        return Task.CompletedTask;
    }

    public async Task ProcessShipment(PaymentConfirmed paymentConfirmed)
    {
        int package_id = 1;

        // TODO better if payment already filters for each shipment
        var items = paymentConfirmed.items;
                    // .Where(x => x.seller_id == sellerId);

        DateTime now = DateTime.UtcNow;

        Shipment shipment = new()
        {
            order_id = paymentConfirmed.orderId,
            customer_id = paymentConfirmed.customer.CustomerId,
            package_count = items.Count,
            total_freight_value = items.Sum(i => i.freight_value),
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

        foreach (var item in items)
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


        // inform seller
        // var sellerActor = GrainFactory.GetGrain<ISellerActor>(this.sellerId);

        // inform customer
        // var custActor = GrainFactory.GetGrain<ICustomerActor>(paymentConfirmed.customer.CustomerId);



        var mark = new TransactionMark(paymentConfirmed.instanceId, TransactionType.CUSTOMER_SESSION, paymentConfirmed.customer.CustomerId, MarkStatus.SUCCESS, "shipment");

        var streamProvider = this.GetStreamProvider(Infra.Constants.DefaultStreamProvider);
        var streamId = StreamId.Create(Infra.Constants.MarkNamespace, Infra.Constants.CheckoutMarkStreamId);
        var stream = streamProvider.GetStream<TransactionMark>(streamId);

        await stream.OnNextAsync(mark);
    }

    public Task UpdateShipment()
    {
        // how to know if this seller has open shipments?
        throw new NotImplementedException();
    }
}


