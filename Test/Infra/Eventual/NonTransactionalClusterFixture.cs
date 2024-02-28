using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrleansApp.Infra;
using Orleans.Serialization;
using Orleans.TestingHost;

namespace Test.Infra.Eventual;

public class NonTransactionalClusterFixture : IDisposable
{
    public TestCluster Cluster { get; private set; }

    private class SiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder hostBuilder)
        {
            hostBuilder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Warning);
            });

            if (ConfigHelper.NonTransactionalDefaultAppConfig.StreamReplication)
            {
                hostBuilder.AddMemoryStreams(Constants.DefaultStreamProvider)
                            .AddMemoryGrainStorage(Constants.DefaultStreamStorage);
            }

            hostBuilder.Services.AddSerializer(ser => { ser.AddNewtonsoftJsonSerializer(isSupported: type => type.Namespace.StartsWith("Common") || type.Namespace.StartsWith("OrleansApp.Abstract")); })
             .AddSingleton(ConfigHelper.NonTransactionalDefaultAppConfig);

            // the non transactional grains need grain storage for persistent state on constructor
            hostBuilder.AddMemoryGrainStorage(Constants.OrleansStorage);

            if (ConfigHelper.NonTransactionalDefaultAppConfig.LogRecords)
                hostBuilder.Services.AddSingleton<IAuditLogger, PostgresAuditLogger>();
            else
                hostBuilder.Services.AddSingleton<IAuditLogger, EtcNullPersistence>();

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
                .AddSingleton(ConfigHelper.NonTransactionalDefaultAppConfig);

            if (ConfigHelper.NonTransactionalDefaultAppConfig.LogRecords)
                clientBuilder.Services.AddSingleton<IAuditLogger, PostgresAuditLogger>();
            else
                clientBuilder.Services.AddSingleton<IAuditLogger, EtcNullPersistence>();
        }
    }

    public NonTransactionalClusterFixture()
    {
        var builder = new TestClusterBuilder(1);
        builder.AddSiloBuilderConfigurator<SiloConfigurator>();
        builder.AddClientBuilderConfigurator<ClientConfigurator>();
        Cluster = builder.Build();
        Cluster.Deploy();
    }

    public void Dispose()
    {
        Cluster.StopAllSilos();
    }

}

