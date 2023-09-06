using Common.Entities;
using Common.Requests;
using Orleans.Interfaces;
using Orleans.TestingHost;

namespace Test;

[Collection(ClusterCollection.Name)]
public class CheckoutWorkflowTest
{
    private readonly TestCluster _cluster;

    private readonly Random random = new Random();

    public CheckoutWorkflowTest(ClusterFixture fixture)
    {
        this._cluster = fixture.Cluster;
    }

    [Fact]
    public async Task Checkout()
    {
        // load customer in customer actor
        var customer = _cluster.GrainFactory.GetGrain<ICustomerActor>(0);
        await customer.SetCustomer(new Customer()
        {
            id = 0,
            first_name = "",
            last_name = "",
            address = "",
            complement = "",
            birth_date = "",
            zip_code = "",
            city = "",
            state = "",
            delivery_count = 0,
            failed_payment_count = 0,
            success_payment_count = 0
        });

        // add correspondent stock items
        var stock1 = _cluster.GrainFactory.GetGrain<IStockActor>(1,"1");
        await stock1.SetItem(new StockItem()
        {
            product_id = 1,
            seller_id = 1,
            qty_available = 10,
            qty_reserved = 0,
            order_count = 0,
            ytd = 1,
        });

        var stock2 = _cluster.GrainFactory.GetGrain<IStockActor>(1,"2");
        await stock2.SetItem(new StockItem()
        {
            product_id = 2,
            seller_id = 1,
            qty_available = 10,
            qty_reserved = 0,
            order_count = 0,
            ytd = 1,
        });

        CustomerCheckout customerCheckout = new()
        {
            CustomerId = 0,
            FirstName = "",
            LastName = "",
            Street = "",
            Complement = "",
            City = "",
            State = "",
            ZipCode = "",
            PaymentType = PaymentType.CREDIT_CARD.ToString(),
            CardNumber = random.Next().ToString(),
            CardHolderName = "",
            CardExpiration = "",
            CardSecurityNumber = "",
            CardBrand = "",
            Installments = 1
        };

        var cart = _cluster.GrainFactory.GetGrain<ICartActor>(0);
        await cart.AddItem(GenerateBasketItem(1,1));
        await cart.AddItem(GenerateBasketItem(1,2));

        await cart.NotifyCheckout(customerCheckout);

        var order = _cluster.GrainFactory.GetGrain<IOrderActor>(0);
        List<Order> orders = await order.GetOrders();

        Assert.Single(orders);

        await customer.Clear();

    }

    private CartItem GenerateBasketItem(int sellerId, int productId)
    {
        return new()
        {
            ProductId = productId,
            SellerId = sellerId,
            UnitPrice = random.Next(),
            // OldUnitPrice = null,
            FreightValue = 0,
            Quantity = 1,
            Voucher = 1
        };
    }

}


