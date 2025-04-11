using Common.Entities;
using Common.Events;
using OrleansApp.Interfaces.SellerView;
using Test.Infra;
using Test.Infra.Transactional;

namespace Test.SellerDashboard;

[Collection(TransactionalClusterCollection.Name)]
public class SellerViewActorTests : BaseTest
{

    public SellerViewActorTests(TransactionalClusterFixture fixture) : base(fixture.Cluster) { }

    [Fact]
    public async Task TestDashboard()
    {
        InitSellerDbContext();

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

        Assert.Single(dashboard.orderEntries);

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

        // as it is a one-way request, the test must wait a little bit more
        await Task.Delay(1000);

        dashboard = await sellerViewActor.QueryDashboard();

        Assert.Empty(dashboard.orderEntries);

        // check if result is none
        Assert.Equal(1, dashboard.sellerView.seller_id);
        Assert.Equal(0, dashboard.sellerView.count_orders);
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

}

