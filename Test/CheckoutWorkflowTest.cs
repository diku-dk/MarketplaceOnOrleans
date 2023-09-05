using System;
using Common.Entities;
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

        var res = await customer.GetCustomer();

        Console.WriteLine("The returned object is: {0}", res);

        Assert.True( res is not null );

        await customer.Clear();

    }

}


