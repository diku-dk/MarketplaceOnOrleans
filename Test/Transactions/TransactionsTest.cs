using Common.Entities;
using Common.Requests;
using Orleans.Transactional;
using Test.Infra;
using Test.Workflows;

namespace Test.Transactions;

[Collection(ClusterCollection.Name)]
public class TransactionsTest : BaseTest
{
    public TransactionsTest(ClusterFixture fixture) : base(fixture){}

    [Fact]
    public async Task TestCheckout()
    {
        await InitData(1, 2);
        await BuildAndSendCheckout();

        var orderActor = _cluster.GrainFactory.GetGrain<ITransactionalOrderActor>(0);
        List<Order> orders = await orderActor.GetOrders();

        Assert.Single(orders);

        var shipmentActor = _cluster.GrainFactory.GetGrain<ITransactionalShipmentActor>(0);
        var shipments = await shipmentActor.GetShipments(0);

        Assert.Single(shipments);
    }

    [Fact]
    public async Task TestDelivery()
    {
        await InitData(1, 2);
        await BuildAndSendCheckout();

        var shipmentActor = _cluster.GrainFactory.GetGrain<ITransactionalShipmentActor>(0);
        // var shipments = await shipmentActor.GetShipments(0);

        await shipmentActor.UpdateShipment(0.ToString());
        var shipments = await shipmentActor.GetShipments(0);

        Assert.True(shipments.Count == 0);
    }

    [Fact]
    public async Task TestPriceUpdate()
    {

        var productActor = _cluster.GrainFactory.GetGrain<ITransactionalProductActor>(1,1.ToString());

        await productActor.SetProduct( new Product()
        {
            seller_id = 1,
            product_id = 1,
            price = 1,
            freight_value = 1,
            active = true,
            version = 1.ToString(),
        });

        PriceUpdate priceUpdate = new PriceUpdate() { price = 10 };

        await productActor.ProcessPriceUpdate(priceUpdate);

        var newPrice = (await productActor.GetProduct()).price;

        Assert.True(newPrice == priceUpdate.price);

    }

    [Fact]
    public async Task TestProductUpdate()
    {

        var productActor = _cluster.GrainFactory.GetGrain<ITransactionalProductActor>(1, 1.ToString());

        await productActor.SetProduct(new Product()
        {
            seller_id = 1,
            product_id = 1,
            price = 1,
            freight_value = 1,
            active = true,
            version = 1.ToString(),
        });

        await productActor.ProcessProductUpdate(new Product()
        {
            seller_id = 1,
            product_id = 1,
            price = 10,
            freight_value = 10,
            active = true,
            version = 2.ToString(),
        });

        var stockActor = _cluster.GrainFactory.GetGrain<ITransactionalStockActor>(1, 1.ToString());

        var version = (await stockActor.GetItem()).version;

        Assert.True(version.SequenceEqual("2"));

    }

}

