using Common.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans.Infra;
using Orleans.Interfaces;
using Orleans.Serialization;
using Orleans.TestingHost;
using Orleans.TransactionalGrains;

namespace Test.Transactions;

public class TransactionalClusterFixture : IDisposable
{
    private class TransactionSiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder hostBuilder) =>
           hostBuilder
              .UseTransactions()
              .AddMemoryGrainStorage(Constants.OrleansStorage)
           .ConfigureLogging(logging =>
           {
               logging.ClearProviders();
               logging.AddConsole();
               logging.SetMinimumLevel(LogLevel.Warning);
           })
           .Services.AddSerializer(ser =>
           {
               ser.AddNewtonsoftJsonSerializer(isSupported: type => type.Namespace.StartsWith("Common"));
           });
    }

    private class SiloConfigurator : ISiloConfigurator
    {
      public void Configure(ISiloBuilder hostBuilder) =>
         hostBuilder
            .AddMemoryGrainStorage(Constants.OrleansStorage)
         .ConfigureLogging(logging =>
         {
             logging.ClearProviders();
             logging.AddConsole();
             logging.SetMinimumLevel(LogLevel.Warning);
         })
         .Services.AddSerializer(ser =>
         {
             ser.AddNewtonsoftJsonSerializer(isSupported: type => type.Namespace.StartsWith("Common"));
         });
    }

    private class ClientConfigurator : IClientBuilderConfigurator
    {
      public void Configure(IConfiguration configuration, IClientBuilder clientBuilder) => 
        clientBuilder
            .UseTransactions()
            .Services.AddSerializer(ser =>
             {
                 ser.AddNewtonsoftJsonSerializer(isSupported: type => type.Namespace.StartsWith("Common"));
             });
    }

    private void Init(bool UseTransactions = false)
    {
        var builder = new TestClusterBuilder();
        if (UseTransactions)
        {
            builder.AddSiloBuilderConfigurator<TransactionSiloConfigurator>();
        }
        else
        {
            builder.AddSiloBuilderConfigurator<SiloConfigurator>();
        }
        builder.AddClientBuilderConfigurator<ClientConfigurator>();
        Cluster = builder.Build();
        Cluster.Deploy();
    }

    public void Dispose()
    {
        Cluster.StopAllSilos();
    }

    private TestCluster Cluster { get; set; }

    [Fact]
    public async Task TestTransactionalStorage()
    {
        Init(true);

        var order = Cluster.GrainFactory.GetGrain<ITransactionalOrderActor>(0);

        var transactionClient = Cluster.Client.ServiceProvider.GetRequiredService<ITransactionClient>();

        await transactionClient.RunTransaction(TransactionOption.Create, async () =>
        {
            await order.TestTransaction(new Order { id = 1, customer_id = 1 });
        });

        Assert.Single((await order.GetOrders()));

        Dispose(); 
    }

    
    [Fact]
    public async Task TestNonTransactionalStorage()
    {
        Init();

        // method calls to transactional storage cannot be made in cases (i) transactions are not activated in the silo and (ii) no transactional client initiates a transaction
        // a possible way is having an interface exclusive to transactional actor, then when transaction is not activated, the non transactional interface canbe called
        var order = Cluster.GrainFactory.GetGrain<IOrderActor>(0);
      
        await order.TestTransaction(new Order { id = 1, customer_id = 1 });
    
        Assert.Single(await order.GetOrders());

        Dispose();
    }
}

