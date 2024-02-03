using Common.Entities;
using Common.Requests;
using Orleans.Interfaces.Replication;
using OrleansApp.Infra;
using OrleansApp.Interfaces;
using Test.Infra;
using Test.Workflows;


// redis-server.exe  redis.windows.conf
// redis-server.exe  redis2.windows.conf
// KEYS *
// get "1-1"
// DEL "1-1"

// dotnet test --filter RedisReplicationTest

namespace Test.Replication;

[Collection(ClusterCollection.Name)]
public class RedisReplicationTest : BaseTest
{
    public RedisReplicationTest(ClusterFixture fixture) : base(fixture) { }


    [Fact]
    public async Task TestReadAfterAddProduct()
    {
        // set product first
        var productActor = _cluster.GrainFactory.GetGrain<IProductActor>(1, 1.ToString());
        var product = new Product()
        {
            seller_id = 1,
            product_id = 1,
            price = 10,
            freight_value = 1,
            active = true,
            version = 1.ToString(),
        };

        await productActor.SetProduct(product);

        var cart1 = _cluster.GrainFactory.GetGrain<ICausalCartActor>(1);
        var productReplica = await cart1.GetReplicaItem(1, 1);

        Assert.True(productReplica.Price == 10 && productReplica.Version == 1.ToString());
    }

    [Fact]
    public async Task TestReadAfterProductUpdate()
    {
        // set product first
        var productActor = _cluster.GrainFactory.GetGrain<IProductActor>(1, 1.ToString());
        var product = new Product()
        {
            seller_id = 1,
            product_id = 1,
            price = 10,
            freight_value = 1,
            active = true,
            version = 1.ToString(),
        };

        await productActor.SetProduct(product);

        // submit product update
        var productUpdated = new Product()
        {
            seller_id = 1,
            product_id = 1,
            price = 10,
            freight_value = 1,
            active = true,
            version = 2.ToString(),
        };

        await productActor.ProcessProductUpdate(productUpdated);

        var cart1 = _cluster.GrainFactory.GetGrain<ICausalCartActor>(1);
        var productReplica = await cart1.GetReplicaItem(1, 1);

        var Product = await productActor.GetProduct();

        Assert.True(productReplica.Price == 10 && productReplica.Version == 2.ToString());
        Assert.True(Product.price == 10 && Product.version == 2.ToString());
    }

    [Fact]
    public async Task TestReadAfterPriceUpdate()
    {
        // set product first
        var productActor = _cluster.GrainFactory.GetGrain<IProductActor>(1, 1.ToString());
        var product = new Product()
        {
            seller_id = 1,
            product_id = 1,
            price = 10,
            freight_value = 1,
            active = true,
            version = 1.ToString(),
        };

        await productActor.SetProduct(product);

        // submit product update
        var priceUpdate = new PriceUpdate()
        {
            sellerId = 1,
            productId = 1,
            price = 1000,
            instanceId = "2"
        };

        await productActor.ProcessPriceUpdate(priceUpdate);

        var cart1 = _cluster.GrainFactory.GetGrain<ICausalCartActor>(1);
        var productReplica = await cart1.GetReplicaItem(1, 1);

        var Product = await productActor.GetProduct();


        Assert.True(productReplica.Price == 1000 && productReplica.Version == 1.ToString());
        Assert.True(Product.price == 1000 && Product.version == 1.ToString());
    }
}