using Common;
using OrleansApp.Infra;
using OrleansApp.Interfaces;
using Test.Infra;

namespace Test.Workflows;

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

        var config = (AppConfig) this._cluster.Client.ServiceProvider.GetService(typeof(AppConfig));
        int shipmentActorId = Helper.GetShipmentActorID(0,config.NumShipmentActors);

        var shipmentActor = this._cluster.GrainFactory.GetGrain<IShipmentActor>(shipmentActorId);

        await shipmentActor.UpdateShipment(1.ToString());

         // should have no shipments
        var shipments = await shipmentActor.GetShipments(0);
        Assert.True(shipments.Count == 0);

        // should have no orders
        var orderActor = _cluster.GrainFactory.GetGrain<IOrderActor>(0);
        Assert.True( ( await orderActor.GetOrders()).Count == 0 );

        await shipmentActor.Reset();
        await orderActor.Reset();
	}

    
    [Fact]
    public async Task ManyCheckoutsDeliveryTest()
    {
        await InitData(1, 2);

        for(int i = 1; i <= numCheckouts; i++){
            await BuildAndSendCheckout();
        }

        int numToRetrieve = numCheckouts - 1;

        var config = (AppConfig) this._cluster.Client.ServiceProvider.GetService(typeof(AppConfig));
        int shipmentActorId = Helper.GetShipmentActorID(0,config.NumShipmentActors);

        var shipmentActor = this._cluster.GrainFactory.GetGrain<IShipmentActor>(shipmentActorId);

        await shipmentActor.UpdateShipment(1.ToString());

        // should have a shipment
        var shipments = await shipmentActor.GetShipments(0);
        Assert.True(shipments.Count == numToRetrieve);

        // should have an order
        var orderActor = _cluster.GrainFactory.GetGrain<IOrderActor>(0);
        Assert.True( ( await orderActor.GetOrders()).Count == numToRetrieve);

        await shipmentActor.Reset();
        await orderActor.Reset();
	}

}  