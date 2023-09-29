using System.Threading.Tasks;
using Orleans.Infra;
using Orleans.Interfaces;
using Orleans.Runtime;
using Orleans.TestingHost;
using Test.Infra;

namespace Test.Transactions;

[Collection(ClusterCollection.Name)]
public class ShipmentTest
{
    private readonly TestCluster _cluster;
    private readonly CheckoutTest checkoutTest;

    public ShipmentTest(ClusterFixture fixture)
	{
        this._cluster = fixture.Cluster;
        this.checkoutTest = new CheckoutTest(fixture); 
	}

    [Fact]
    public async Task DeliveryTest()
    {
        await checkoutTest.Checkout();

        int shipmentActorId = Helper.GetShipmentActorID(0);

        var shipment = _cluster.GrainFactory.GetGrain<IShipmentActor>(shipmentActorId);

        await shipment.UpdateShipment("1");

         // should have no shipments
        var shipmentActor = _cluster.GrainFactory.GetGrain<IShipmentActor>(Helper.GetShipmentActorID(0));
        var shipments = await shipmentActor.GetShipments(0);
        Assert.True(shipments.Count == 0);

        // should have no orders
        var orderActor = _cluster.GrainFactory.GetGrain<IOrderActor>(0);
        Assert.True( ( await orderActor.GetOrders()).Count == 0 );
	}

    [Fact]
    public async Task ResetTest()
    {
        await checkoutTest.Checkout();

        var mgmt = _cluster.GrainFactory.GetGrain<IManagementGrain>(0);
        var stats = await mgmt.GetSimpleGrainStatistics();

        var grain = _cluster.GrainFactory.GetGrain<IShipmentActor>(0);
        await grain.Reset();

    }

}  