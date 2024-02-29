using Common.Config;
using Common.Entities;
using Common.Requests;
using OrleansApp.Infra;
using OrleansApp.Interfaces;
using Test.Infra;
using Test.Infra.Eventual;

namespace Test.Workflows;

[Collection(NonTransactionalClusterCollection.Name)]
public class CheckoutTest : BaseTest
{
    public CheckoutTest(NonTransactionalClusterFixture fixture) : base(fixture.Cluster) { }

    [Fact]
    public async Task Checkout()
    {
        // make sure transactions is not activated
        var config = (AppConfig)_cluster.Client.ServiceProvider.GetService(typeof(AppConfig));
        //config.OrleansTransactions = false;

        int customerId = 1;
        await InitStorage();
        await InitData(1, 2);
        await BuildAndSendCheckout(customerId);

        var orderActor = _cluster.GrainFactory.GetGrain<IOrderActor>(customerId);
        List<Order> orders = await orderActor.GetOrders();

        Assert.Single(orders);
 
        int shipmentActorId = Helper.GetShipmentActorID(customerId, config.NumShipmentActors);
        var shipmentActor = _cluster.GrainFactory.GetGrain<IShipmentActor>(shipmentActorId);
        var shipments = await shipmentActor.GetShipments(customerId);
        var count = shipments.Count;
        Assert.True(count == 1);

        await shipmentActor.Reset();
        await orderActor.Reset();
    }

    [Fact]
    public async Task CheckoutTwoOrdersSameCustomer()
    {
        var config = (AppConfig)_cluster.Client.ServiceProvider.GetService(typeof(AppConfig));

        int customerId = 1;

        await InitStorage();
        await InitData(1, 2);

        var item1 = GenerateCartItem(1, 1);
        var item2 = GenerateCartItem(1, 2);

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

        for (var i = 0; i < 2; i++)
        {
            await cart.AddItem(item1);
            await cart.AddItem(item2);
            await cart.NotifyCheckout(customerCheckout);
        }

        var orderActor = _cluster.GrainFactory.GetGrain<IOrderActor>(customerId);
        var numOrders = await orderActor.GetNumOrders();
        Console.WriteLine("[CheckoutTwoOrdersSameCustomer] Customer ID {0} Count {1}", 0, numOrders);
        Assert.True(2 == numOrders);
        await orderActor.Reset();

        int shipmentActorId = Helper.GetShipmentActorID(customerId, config.NumShipmentActors);
        var shipmentActor = _cluster.GrainFactory.GetGrain<IShipmentActor>(shipmentActorId);
        var shipments = await shipmentActor.GetShipments(customerId);
        var count = shipments.Count;
        Assert.True(count == 2);

        await shipmentActor.Reset();
    }

    [Fact]
    public async Task CheckoutTwoOrdersDifferentCustomers()
    {
        var config = (AppConfig)_cluster.Client.ServiceProvider.GetService(typeof(AppConfig));

        var numCustomers = 2;
        await InitStorage();
        await InitData(numCustomers, 2);

        var tasks = new List<Task>();
        for (var customerId = 1; customerId < numCustomers; customerId++)
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

        int shipmentActorId = Helper.GetShipmentActorID(0, config.NumShipmentActors);
        var shipmentActor = _cluster.GrainFactory.GetGrain<IShipmentActor>(shipmentActorId);
        for (var customerId = 1; customerId < numCustomers; customerId++)
        {
            var shipments = await shipmentActor.GetShipments(customerId);
            var numShipments = shipments.Count;
            Console.WriteLine("[CheckoutTwoOrdersDifferentCustomers] Customer ID {0} Count {1}", customerId, numShipments);
            Assert.True(numShipments == 1);
            var orderActor = _cluster.GrainFactory.GetGrain<IOrderActor>(customerId);
            var numOrders = await orderActor.GetNumOrders();
            Console.WriteLine("[CheckoutTwoOrdersDifferentCustomers] Customer ID {0} numOrders {1}", customerId, numOrders);
            await orderActor.Reset();
        }
        // clean so other tests do not fail
        await shipmentActor.Reset();
    }

    async Task InitStorage()
    {
        IAuditLogger persistence = (IAuditLogger)_cluster.Client.ServiceProvider.GetService(typeof(IAuditLogger));
        if (ConfigHelper.TransactionalDefaultAppConfig.LogRecords)
        {
            await persistence.SetUpLog();
            await persistence.CleanLog();
        }
        if (ConfigHelper.TransactionalDefaultAppConfig.AdoNetGrainStorage)
            await persistence.TruncateStorage();
    }

}


