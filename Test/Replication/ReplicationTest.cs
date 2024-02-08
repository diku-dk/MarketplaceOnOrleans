using Common.Entities;
using Common.Requests;
using Orleans.Interfaces.Replication;
using OrleansApp.Transactional;
using Test.Infra;
using Test.Infra.Transactional;

namespace Test.Replication;

[Collection(TransactionalClusterCollection.Name)]
public class ReplicationTest : BaseTest
{
    public ReplicationTest(TransactionalClusterFixture fixture) : base(fixture.Cluster){}

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

        var cartActor = _cluster.GrainFactory.GetGrain<IEventualCartActor>(1);
        CartItem cartItem = new CartItem() {
            SellerId = 1,
            ProductId = 1,
            ProductName = "",
            UnitPrice = 0,
            Quantity = 1,
            Voucher = 0,
            Version = 1.ToString(),
            FreightValue = 0
        };
        //will subscribe to product [1,1] updates
        await cartActor.AddItem(cartItem);

        PriceUpdate priceUpdate = new PriceUpdate() { price = 10 };

        await productActor.ProcessPriceUpdate(priceUpdate);

        var newPrice = (await productActor.GetProduct()).price;

        // await replication
        await Task.Delay(1000);
        var cartPrice = (await cartActor.GetReplicaItem(1, 1)).price;

        Assert.True(newPrice == priceUpdate.price);
        Assert.True(newPrice == cartPrice);
    }

}

