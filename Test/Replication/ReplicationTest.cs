using Common.Entities;
using Common.Requests;
using Orleans.Interfaces.Replication;
using OrleansApp.Grains;
using OrleansApp.Interfaces;
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
        int productId = 100;
        int customerId = 100;

        var productActor = _cluster.GrainFactory.GetGrain<ITransactionalProductActor>(1, productId.ToString());

        await productActor.SetProduct( new Product()
        {
            seller_id = 1,
            product_id = productId,
            price = 1,
            freight_value = 1,
            active = true,
            version = 1.ToString(),
        });

        var cartActor = _cluster.GrainFactory.GetGrain<IEventualCartActor>(customerId);
        CartItem cartItem = new CartItem() {
            SellerId = 1,
            ProductId = productId,
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
        var cartPrice = (await cartActor.GetReplicaItem(1, productId)).price;

        Assert.True(newPrice == priceUpdate.price);
        Assert.True(newPrice == cartPrice);

        await cartActor.Seal();
    }

    [Fact]
    public async Task TestTrackHistory()
    {
        int maxCustomers = 10;
        await InitData(maxCustomers, 2);

        for(int i = 1; i <= maxCustomers; i++){
            await BuildAndSendCheckout(i);
        }

        for(int i = 1; i <= maxCustomers; i++){
            var cartActor = _cluster.GrainFactory.GetGrain<ICartActor>(i);
            var carts = await cartActor.GetHistory(i.ToString());
            Assert.NotEmpty(carts);
            await cartActor.Seal();
        }  

    }

}

