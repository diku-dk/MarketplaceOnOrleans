using Common.Entities;
using Common.Requests;
using Orleans.Infra;
using Orleans.Interfaces;
using Test.Infra;

namespace Test.Transactions;

[Collection(ClusterCollection.Name)]
public class CheckoutTest : BaseTest
{
    public CheckoutTest(ClusterFixture fixture): base(fixture){ }

    [Fact]
    public async Task Checkout()
    {
        await InitStorage();
        await InitData(1, 2);
        await BuildAndSendCheckout();

        var order = _cluster.GrainFactory.GetGrain<IOrderActor>(0);
        List<Order> orders = await order.GetOrders();

        Assert.Single(orders);

        var shipmentActor = _cluster.GrainFactory.GetGrain<IShipmentActor>(Helper.GetShipmentActorID(0,1));
        var shipments = (await shipmentActor.GetShipments(0));
        var count = shipments.Count;
        Assert.True(count == 1);
    }

    [Fact]
    public async Task CheckoutTwoOrdersSameCustomer()
    {
        await InitStorage();
        await InitData(1, 2);

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

        var shipmentActor = _cluster.GrainFactory.GetGrain<IShipmentActor>(Helper.GetShipmentActorID(0,1));
        var shipments = (await shipmentActor.GetShipments(0));
        var count = shipments.Count;
        Assert.True(count == 2);
    }

    [Fact]
    public async Task CheckoutTwoOrdersDifferentCustomers()
    {
        var numCustomer = 2;
        await InitStorage();
        await InitData(numCustomer, 2);

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
            var shipmentActor = _cluster.GrainFactory.GetGrain<IShipmentActor>(Helper.GetShipmentActorID(customerId, ClusterFixture.NumShipmentActors));
            var shipments = (await shipmentActor.GetShipments(customerId));
            var count = shipments.Count;
            Console.WriteLine("Customer ID {0} Count {1}", customerId, count);
            Assert.True(count == 1);
        }
    }

    async Task InitStorage()
    {
        IPersistence persistence = (IPersistence) _cluster.ServiceProvider.GetService(typeof(IPersistence));
        await persistence.SetUpLog();
        await persistence.CleanLog();
        await persistence.TruncateStorage();
    }

}


