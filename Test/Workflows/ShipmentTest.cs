using Common.Config;
using OrleansApp.Infra;
using OrleansApp.Interfaces;
using Test.Infra;
using Test.Infra.Eventual;

namespace Test.Workflows;

[Collection(NonTransactionalClusterCollection.Name)]
public class ShipmentTest : BaseTest
{
    private int numCheckouts = 10;

    public ShipmentTest(NonTransactionalClusterFixture fixture) : base(fixture.Cluster) {}

    [Fact]
    public async Task SimpleDeliveryTest()
    {
        var config = (AppConfig)this._cluster.Client.ServiceProvider.GetService(typeof(AppConfig));

        int customerId = 1;
        await InitData(1,2);
        await BuildAndSendCheckout(customerId);

        int shipmentActorId = Helper.GetShipmentActorID(customerId, config.NumShipmentActors);

        var shipmentActor = this._cluster.GrainFactory.GetGrain<IShipmentActor>(shipmentActorId);

        await shipmentActor.UpdateShipment(1.ToString());

         // should have no shipments
        var shipments = await shipmentActor.GetShipments(0);
        Assert.Empty(shipments);

        // should have no orders
        var orderActor = this._cluster.GrainFactory.GetGrain<IOrderActor>(customerId);
        Assert.Empty(await orderActor.GetOrders());

        await shipmentActor.Reset();
        await orderActor.Reset();
    }

    
    [Fact]
    public async Task ManyCheckoutsDeliveryTest()
    {
        var config = (AppConfig)this._cluster.Client.ServiceProvider.GetService(typeof(AppConfig));

        int customerId = 1;
        await this.InitData(customerId, 2);

        for(int i = 1; i <= this.numCheckouts; i++){
            await this.BuildAndSendCheckout(customerId);
        }

        int numToRetrieve = this.numCheckouts - 1;

        int shipmentActorId = Helper.GetShipmentActorID(customerId, config.NumShipmentActors);

        var shipmentActor = this._cluster.GrainFactory.GetGrain<IShipmentActor>(shipmentActorId);

        await shipmentActor.UpdateShipment(1.ToString());
        
        // should have a shipment
        var shipments = await shipmentActor.GetShipments(customerId);
        Assert.True(shipments.Count == numToRetrieve);

        // should have an order
        var orderActor = this._cluster.GrainFactory.GetGrain<IOrderActor>(customerId);
        Assert.True((await orderActor.GetOrders()).Count == numToRetrieve);
   
        await shipmentActor.Reset();
        await orderActor.Reset();
    }

}  