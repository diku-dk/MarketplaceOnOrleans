using Orleans.Infra;
using Orleans.Interfaces;
using Test.Infra;

namespace Test.Transactions;

[Collection(ClusterCollection.Name)]
public class ShipmentTest : BaseTest
{

    int numCheckouts = 10;

    public ShipmentTest(ClusterFixture fixture) : base(fixture) {}

    [Fact]
    public async Task SimpleDeliveryTest()
    {
        await InitData(1,2);
        await BuildAndSendCheckout();

        int shipmentActorId = Helper.GetShipmentActorID(0,1);

        var shipment = this._cluster.GrainFactory.GetGrain<IShipmentActor>(shipmentActorId);

        await shipment.UpdateShipment(1.ToString());

         // should have no shipments
        var shipmentActor = this._cluster.GrainFactory.GetGrain<IShipmentActor>(Helper.GetShipmentActorID(0,1));
        var shipments = await shipmentActor.GetShipments(0);
        Assert.True(shipments.Count == 0);

        // should have no orders
        var orderActor = _cluster.GrainFactory.GetGrain<IOrderActor>(0);
        Assert.True( ( await orderActor.GetOrders()).Count == 0 );
	}

    
    [Fact]
    public async Task ManyCheckoutsDeliveryTest()
    {
        await InitData(1, 2);

        for(int i = 1; i <= numCheckouts; i++){
            await BuildAndSendCheckout();
        }

        int numToRetrieve = numCheckouts - 1;

        int shipmentActorId = Helper.GetShipmentActorID(0,1);

        var shipment = this._cluster.GrainFactory.GetGrain<IShipmentActor>(shipmentActorId);

        await shipment.UpdateShipment(1.ToString());

        // should have a shipment
        var shipmentActor = this._cluster.GrainFactory.GetGrain<IShipmentActor>(Helper.GetShipmentActorID(0,ClusterFixture.NumShipmentActors));
        var shipments = await shipmentActor.GetShipments(0);
        Assert.True(shipments.Count == numToRetrieve);

        // should have an order
        var orderActor = _cluster.GrainFactory.GetGrain<IOrderActor>(0);
        Assert.True( ( await orderActor.GetOrders()).Count == numToRetrieve);
	}

    [Fact]
    public async Task ResetTest()
    {
        
        // var mgmt = _cluster.GrainFactory.GetGrain<IManagementGrain>(0);
        // var stats = await mgmt.GetSimpleGrainStatistics();

        var grain = _cluster.GrainFactory.GetGrain<IShipmentActor>(0);
        await grain.Reset();

    }

}  