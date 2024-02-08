using Common.Entities;
using Common.Events;
using Common.Integration;
using Microsoft.EntityFrameworkCore;
using Orleans.Interfaces.SellerView;
using SellerMS.Infra;
using Test.Infra;
using Test.Infra.Transactional;

namespace Test.SellerDashboard;

[Collection(TransactionalClusterCollection.Name)]
public class SellerViewActorTests : BaseTest
{

    public SellerViewActorTests(TransactionalClusterFixture fixture) : base(fixture.Cluster) { }

    private SellerDbContext Init()
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

        return context;
    }

    [Fact]
    public async void TestInsertionsToOrderEntries()
    {
        Init();

        var orderList = new List<OrderItem>
        {
            GenerateOrderItem(1, 1)
        };

        InvoiceIssued invoiceIssued = new InvoiceIssued()
        {
            customer = base.BuildCustomerCheckout(1),
            orderId = 1,
            invoiceNumber = "test",
            issueDate = DateTime.Today,
            totalInvoice = 1,
            items = orderList
        };

        var sellerViewActor = this._cluster.GrainFactory.GetGrain<ISellerViewActor>(1);

        await sellerViewActor.ProcessNewInvoice(invoiceIssued);

        ShipmentNotification shipmentNotification = new ShipmentNotification()
        {
            customerId = 1,
            orderId = 1,
            eventDate = DateTime.Today, 
            instanceId = "1",
            status = ShipmentStatus.approved
        };

        await sellerViewActor.ProcessShipmentNotification(shipmentNotification);

        var dashboard = await sellerViewActor.QueryDashboard();

        Assert.True(dashboard.orderEntries.Count == 1);

        await sellerViewActor.ProcessDeliveryNotification(new DeliveryNotification()
        {
           customerId = 1,
           orderId = 1,
           packageId = 1,
           sellerId = 1,
           productId = 1,
           productName = "test",
           status = PackageStatus.delivered,
           deliveryDate = DateTime.Now,
           instanceId = "1"
        });

        await sellerViewActor.ProcessShipmentNotification(new ShipmentNotification()
        {
            customerId = 1,
            orderId = 1,
            eventDate = DateTime.Now, 
            instanceId = "1",
            status = ShipmentStatus.concluded
        });

        dashboard = await sellerViewActor.QueryDashboard();

        Assert.True(dashboard.orderEntries.Count == 0);
    }

    protected OrderItem GenerateOrderItem(int sellerId, int productId)
    {
        return new()
        {
            product_id = productId,
            seller_id = sellerId,
            unit_price = 1,
            freight_value = 1,
            quantity = 1,
            voucher = 1,
            product_name = "test",
            total_amount = 1,
            total_items = 1,
            shipping_limit_date = DateTime.Today,
            order_id = 1,
            order_item_id = 1,
        };
    }

    [Fact]
    public void TestDashboard()
    {
        var context = Init();

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

        context.Database.ExecuteSqlRaw(SellerDbContext.RefreshMaterializedView);

        var res2 = context.OrderEntries.Where(oe => oe.seller_id == 1).ToList();
        Assert.True(res2.Count() == 10);

        IQueryable<OrderSellerView> queryableSellerView = context.OrderSellerView.Where(oe => oe.seller_id == 1);
        OrderSellerView sellerView = queryableSellerView.First();

        var test = context.OrderSellerView.ToList();

        Assert.True(sellerView is not null);
        Assert.True(test.Count == 1);
    }

}

