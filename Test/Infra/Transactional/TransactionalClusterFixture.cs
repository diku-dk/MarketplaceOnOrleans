using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrleansApp.Infra;
using Orleans.Serialization;
using Orleans.TestingHost;
using OrleansApp.Infra.Redis;
using OrleansApp.Infra.SellerDb;
using OrleansApp.Service;

namespace Test.Infra.Transactional;

/**
* https://learn.microsoft.com/en-us/dotnet/orleans/tutorials-and-samples/testing
* https://stackoverflow.com/questions/70640638/how-to-configure-testclusterbuilder-such-that-the-test-cluster-has-access-to-sms
*/
public sealed class TransactionalClusterFixture : IDisposable
{
    public TestCluster Cluster { get; private set; }

    private class SiloConfigurator : ISiloConfigurator
    {
        // https://stackoverflow.com/questions/55497800/populate-iconfiguration-for-unit-tests
        public void Configure(ISiloBuilder hostBuilder)
        {
            hostBuilder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Warning);
            });

            if (ConfigHelper.TransactionalDefaultAppConfig.SellerViewPostgres)
            {
                hostBuilder.Services.AddDbContextFactory<SellerDbContext>();
                hostBuilder.Services.AddSingleton<IShipmentService, CustomShipmentServiceImpl>();
            } else
            {
                hostBuilder.Services.AddSingleton<IShipmentService, DefaultShipmentServiceImpl>();
            }

            if (ConfigHelper.TransactionalDefaultAppConfig.StreamReplication)
            {
                hostBuilder.AddMemoryStreams(Constants.DefaultStreamProvider)
                            .AddMemoryGrainStorage(Constants.DefaultStreamStorage);
            }
            
            if (ConfigHelper.TransactionalDefaultAppConfig.RedisReplication)
            {
                hostBuilder.Services.AddSingleton<IRedisConnectionFactory>(new RedisConnectionFactoryImpl(ConfigHelper.TransactionalDefaultAppConfig.RedisPrimaryConnectionString, ConfigHelper.TransactionalDefaultAppConfig.RedisSecondaryConnectionString));
            } else
            {
                hostBuilder.Services.AddSingleton<IRedisConnectionFactory>(new EtcNullConnectionFactoryImpl());
            }

            if (ConfigHelper.TransactionalDefaultAppConfig.OrleansTransactions)
            {
                if (ConfigHelper.TransactionalDefaultAppConfig.AdoNetGrainStorage) { 

                    hostBuilder.AddAdoNetGrainStorage(Constants.OrleansStorage, options =>
                    {
                        options.Invariant = "Npgsql";
                        options.ConnectionString = ConfigHelper.PostgresConnectionString;
                    });
                }
                else
                {
                    hostBuilder.AddMemoryGrainStorage(Constants.OrleansStorage);
                }
                hostBuilder.UseTransactions();
            }

            hostBuilder.Services.AddSerializer(ser => { ser.AddNewtonsoftJsonSerializer(isSupported: type => type.Namespace.StartsWith("Common") || type.Namespace.StartsWith("OrleansApp.Abstract")); })
             .AddSingleton(ConfigHelper.TransactionalDefaultAppConfig);

            if (ConfigHelper.TransactionalDefaultAppConfig.LogRecords)
            {
                hostBuilder.Services.AddSingleton<IAuditLogger, PostgresAuditLogger>();
            }
            else
            {
                hostBuilder.Services.AddSingleton<IAuditLogger, EtcNullPersistence>();
            }
        }
    }

    private class ClientConfigurator : IClientBuilderConfigurator
    {
        public void Configure(IConfiguration configuration, IClientBuilder clientBuilder)
        {
            clientBuilder
                .Services.AddSerializer(ser =>
                 {
                     ser.AddNewtonsoftJsonSerializer(isSupported: type => type.Namespace.StartsWith("Common") || type.Namespace.StartsWith("OrleansApp.Abstract"));
                 })
                .AddSingleton(ConfigHelper.TransactionalDefaultAppConfig);

            clientBuilder.UseTransactions();

            if (ConfigHelper.TransactionalDefaultAppConfig.LogRecords){
                clientBuilder.Services.AddSingleton<IAuditLogger, PostgresAuditLogger>();
            }
            else
            {
                clientBuilder.Services.AddSingleton<IAuditLogger, EtcNullPersistence>();
            }

            // for tests
            if (ConfigHelper.TransactionalDefaultAppConfig.SellerViewPostgres)
            {
                clientBuilder.Services.AddDbContextFactory<SellerDbContext>();
                clientBuilder.Services.AddSingleton<IShipmentService, CustomShipmentServiceImpl>();
            } else
            {
                 clientBuilder.Services.AddSingleton<IShipmentService, DefaultShipmentServiceImpl>();
            }
        }
    }

    public TransactionalClusterFixture()
    {
        var builder = new TestClusterBuilder(1);
        builder.AddSiloBuilderConfigurator<SiloConfigurator>();
        builder.AddClientBuilderConfigurator<ClientConfigurator>();
        this.Cluster = builder.Build();
        this.Cluster.Deploy();
    }

    public void Dispose()
    {
        this.Cluster.StopAllSilos();
    }

}

