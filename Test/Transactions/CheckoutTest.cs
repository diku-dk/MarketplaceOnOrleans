using Common.Entities;
using Common.Requests;
using Orleans.Infra;
using Orleans.Interfaces;
using Orleans.TestingHost;
using Test.Infra;

namespace Test.Transactions;

[Collection(ClusterCollection.Name)]
public class CheckoutTest
{
    private readonly TestCluster _cluster;

    private readonly Random random = new Random();

    public CheckoutTest(ClusterFixture fixture)
    {
        this._cluster = fixture.Cluster;
    }

    [Fact]
    public async Task Checkout()
    {
        await Init(1, 2);

        CustomerCheckout customerCheckout = new()
        {
            CustomerId = 0,
            FirstName = "Customer",
            LastName = "Test",
            Street = "Some unknown street",
            Complement = "Still unknown",
            City = "City of Dreams",
            State = "Orleans",
            ZipCode = "12345",
            PaymentType = PaymentType.CREDIT_CARD.ToString(),
            CardNumber = random.Next().ToString(),
            CardHolderName = "Name",
            CardExpiration = "1224",
            CardSecurityNumber = "001",
            CardBrand = "VISA",
            Installments = 1
        };

        var cart = _cluster.GrainFactory.GetGrain<ICartActor>(0);
        await cart.AddItem(GenerateCartItem(1,1));
        await cart.AddItem(GenerateCartItem(1,2));

        await cart.NotifyCheckout(customerCheckout);

        var order = _cluster.GrainFactory.GetGrain<IOrderActor>(0);
        List<Order> orders = await order.GetOrders();

        Assert.Single(orders);

        var shipmentActor = _cluster.GrainFactory.GetGrain<IShipmentActor>(Helper.GetShipmentActorID(0));
        var shipments = (await shipmentActor.GetShipments(0));
        var count = shipments.Count;
        Assert.True(count == 1);
    }

    [Fact]
    public async Task CheckoutTwoOrdersSameCustomer()
    {
        await Init(1, 2);

        var item1 = GenerateCartItem(1, 1);
        var item2 = GenerateCartItem(1, 2);

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

        for (var i = 0; i < 2; i++)
        {
            await cart.AddItem(item1);
            await cart.AddItem(item2);
            await cart.NotifyCheckout(customerCheckout);
        }

        var order = _cluster.GrainFactory.GetGrain<IOrderActor>(0);
        Assert.True(2 == await order.GetNumOrders());

        var shipmentActor = _cluster.GrainFactory.GetGrain<IShipmentActor>(Helper.GetShipmentActorID(0));
        var shipments = (await shipmentActor.GetShipments(0));
        var count = shipments.Count;
        Assert.True(count == 2);
    }

    [Fact]
    public async Task CheckoutTwoOrdersDifferentCustomers()
    {
        var numCustomer = 2;
        await Init(numCustomer, 2);

        var tasks = new List<Task>();
        for (var customerId = 0; customerId < numCustomer; customerId++)
        {
            CustomerCheckout customerCheckout = new()
            {
                CustomerId = customerId,
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

            var cart = _cluster.GrainFactory.GetGrain<ICartActor>(customerId);
            await cart.AddItem(GenerateCartItem(1, 1));
            await cart.AddItem(GenerateCartItem(1, 2));
            tasks.Add(cart.NotifyCheckout(customerCheckout));
        }
        await Task.WhenAll(tasks);

        for (var customerId = 0; customerId < numCustomer; customerId++)
        {
            var shipmentActor = _cluster.GrainFactory.GetGrain<IShipmentActor>(Helper.GetShipmentActorID(customerId));
            var shipments = (await shipmentActor.GetShipments(customerId));
            var count = shipments.Count;
            Console.WriteLine("Customer ID {0} Count {1}", customerId, count);
            Assert.True(count == 1);
        }
    }

    async Task Init(int numCustomer, int numStockItem)
    {
        DBHelper.SetUpLog();
        DBHelper.CleanLog();
        DBHelper.TruncateOrleansStorage();

        // load customer in customer actor
        for (var customerId = 0; customerId < numCustomer; customerId++)
        {
            var customer = _cluster.GrainFactory.GetGrain<ICustomerActor>(customerId);
            await customer.SetCustomer(new Customer()
            {
                id = customerId,
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
        }

        // add correspondent stock items
        for (var itemId = 1; itemId <= numStockItem; itemId++)
        {
            var stock1 = _cluster.GrainFactory.GetGrain<IStockActor>(1, itemId.ToString());
            await stock1.SetItem(new StockItem()
            {
                product_id = itemId,
                seller_id = 1,
                qty_available = 10,
                qty_reserved = 0,
                order_count = 0,
                ytd = 1,
                version = 1
            });
        }
    }

    private CartItem GenerateCartItem(int sellerId, int productId)
    {
        return new()
        {
            ProductId = productId,
            SellerId = sellerId,
            UnitPrice = random.Next(),
            FreightValue = 1,
            Quantity = 1,
            Voucher = 1,
            ProductName = "test",
            Version = 1
        };
    }

}


