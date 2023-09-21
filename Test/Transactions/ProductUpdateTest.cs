using Common.Entities;
using Orleans.Infra;
using Orleans.Interfaces;
using Orleans.TestingHost;
using Test.Infra;

namespace Test.Transactions;

[Collection(ClusterCollection.Name)]
public class ProductUpdateTest
{
    private readonly TestCluster _cluster;

	public ProductUpdateTest(ClusterFixture fixture)
	{
        this._cluster = fixture.Cluster;
	}

    [Fact]
    public async Task ProductUpdate()
    {
        DBHelper.TruncateOrleansStorage();

        // set product first
        var productActor = _cluster.GrainFactory.GetGrain<IProductActor>(1,"1");
        var product = new Product()
        {
            seller_id = 1,
            product_id = 1,
            price = 10,
            freight_value = 1,
            active = true,
            version = 1,
        };

        await productActor.SetProduct(product);

        // set corresponding stock item
        var item = new StockItem()
        {
            product_id = 1,
            seller_id = 1,
            qty_available = 10,
            qty_reserved = 0,
            order_count = 0,
            ytd = 1,
            version = 1
        };

        var stock1 = _cluster.GrainFactory.GetGrain<IStockActor>(1,"1");
        await stock1.SetItem(item);

        // submit product update
        var productUpdated = new Product(){
            seller_id = 1,
            product_id = 1,
            price = 10,
            freight_value = 1,
            active = true,
            version = 2,
        };

        await productActor.ProcessProductUpdate(productUpdated);

        var newItem = await stock1.GetItem();

        Assert.True( newItem.version == 2 );
    }
}