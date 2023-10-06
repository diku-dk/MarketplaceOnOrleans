using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans.Infra;
using Orleans.Serialization;
using Orleans.TestingHost;

namespace Test.Infra;

/**
* https://learn.microsoft.com/en-us/dotnet/orleans/tutorials-and-samples/testing
* https://stackoverflow.com/questions/70640638/how-to-configure-testclusterbuilder-such-that-the-test-cluster-has-access-to-sms
*/
public class ClusterFixture : IDisposable
{
    public TestCluster Cluster { get; private set; }

    private class SiloConfigurator : ISiloConfigurator
    {
        // https://stackoverflow.com/questions/55497800/populate-iconfiguration-for-unit-tests
        public void Configure(ISiloBuilder hostBuilder) {

            hostBuilder
             .ConfigureLogging(logging =>
             {
                 logging.ClearProviders();
                 logging.AddConsole();
                 logging.SetMinimumLevel(LogLevel.Warning);
             })
             .Services.AddSerializer(ser =>
             {
                 ser.AddNewtonsoftJsonSerializer(isSupported: type => type.Namespace.StartsWith("Common"));
             })
             .AddSingleton(ConfigHelper.DefaultAppConfig)
             ;

            if (ConfigHelper.DefaultAppConfig.AdoNetGrainStorage)
            {
                hostBuilder.AddAdoNetGrainStorage(Constants.OrleansStorage, options =>
                 {
                     options.Invariant = "Npgsql";
                     options.ConnectionString = ConfigHelper.PostgresConnectionString;
                 });
            } else
            {
                hostBuilder.AddMemoryGrainStorage(Constants.OrleansStorage);
                
            }
            if(ConfigHelper.DefaultAppConfig.LogRecords)
                hostBuilder.Services.AddSingleton<IPersistence, PostgreSQLPersistence>();
            else
                hostBuilder.Services.AddSingleton<IPersistence, EtcNullPersistence>();

        }
    }

    private class ClientConfigurator : IClientBuilderConfigurator
    {
      public void Configure(IConfiguration configuration, IClientBuilder clientBuilder) {
        clientBuilder
            .Services.AddSerializer(ser =>
             {
                 ser.AddNewtonsoftJsonSerializer(isSupported: type => type.Namespace.StartsWith("Common"));
             }).AddSingleton(ConfigHelper.DefaultAppConfig);
             if(ConfigHelper.DefaultAppConfig.LogRecords)
                clientBuilder.Services.AddSingleton<IPersistence, PostgreSQLPersistence>();
             else
                clientBuilder.Services.AddSingleton<IPersistence, EtcNullPersistence>();
        }
    }

    public ClusterFixture()
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

