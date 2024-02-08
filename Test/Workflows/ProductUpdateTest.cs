using Common.Entities;
using OrleansApp.Infra;
using OrleansApp.Interfaces;
using Test.Infra;
using Test.Infra.Eventual;

namespace Test.Workflows;

[Collection(NonTransactionalClusterCollection.Name)]
public class ProductUpdateTest : BaseTest
{

    public ProductUpdateTest(NonTransactionalClusterFixture fixture) : base(fixture.Cluster) { }

    [Fact]
    public async Task ProductUpdate()
    {
        IAuditLogger persistence = (IAuditLogger)_cluster.ServiceProvider.GetService(typeof(IAuditLogger));
        await persistence.TruncateStorage();

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

        // set corresponding stock item
        var item = new StockItem()
        {
            product_id = 1,
            seller_id = 1,
            qty_available = 10,
            qty_reserved = 0,
            order_count = 0,
            ytd = 1,
            version = 1.ToString()
        };

        var stock1 = _cluster.GrainFactory.GetGrain<IStockActor>(1, 1.ToString());
        await stock1.SetItem(item);

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

        var newItem = await stock1.GetItem();

        Assert.True(newItem.version.SequenceEqual(2.ToString()));
    }
}