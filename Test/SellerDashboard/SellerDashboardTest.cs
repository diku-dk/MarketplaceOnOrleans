
using Common.Entities;
using Common.Integration;
using Microsoft.EntityFrameworkCore;
using SellerMS.Infra;
using Test.Infra.Transactional;
using Test.Workflows;

namespace Test.SellerDashboard;

[Collection(TransactionalClusterCollection.Name)]
public class SellerDashboardTest : BaseTest
{

    public SellerDashboardTest(TransactionalClusterFixture fixture) : base(fixture.Cluster) { }

    [Fact]
    public void TestDashboard()
    {
        // ensure schema is created

        SellerDbContext context = (SellerDbContext) _cluster.Client.ServiceProvider.GetService(typeof(SellerDbContext));
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        context.Database.Migrate();

        // ensure views are created
        context.Database.ExecuteSqlRaw(SellerDbContext.OrderSellerViewSql);
        context.Database.ExecuteSqlRaw(SellerDbContext.OrderSellerViewSqlIndex);

        // truncate previous records
        context.OrderEntries.ExecuteDelete();

        // create entries
        for(int i = 1; i <= 10; i++) { 
            OrderEntry oe = new OrderEntry()
            {
                customer_id = 1,
                order_id = 1,
                product_id = i,
                seller_id = 1,
                package_id = i,
                product_name = "test",
                product_category = "test",
                unit_price = 1,
                quantity = 1,
                total_items = 1,
                total_amount = 1,
                total_incentive = 1,
                total_invoice = 1,
                freight_value = 1,
                shipment_date = new DateTime(),
                delivery_date = new DateTime(),
                order_status = OrderStatus.IN_TRANSIT,
                delivery_status = PackageStatus.shipped
            };
            context.OrderEntries.Add(oe);
        }

        context.SaveChanges();

        context.Database.ExecuteSqlRaw($"REFRESH MATERIALIZED VIEW CONCURRENTLY public.order_seller_view;");

        var res2 = context.OrderEntries.Where(oe => oe.seller_id == 1).ToList();
        Assert.True(res2.Count() == 10);

        IQueryable<OrderSellerView> queryableSellerView = context.OrderSellerView.Where(oe => oe.seller_id == 1);
        OrderSellerView sellerView = queryableSellerView.First();

        var test = context.OrderSellerView.ToList();

        Assert.True(sellerView is not null);
        Assert.True(test.Count == 1);
    }

}

