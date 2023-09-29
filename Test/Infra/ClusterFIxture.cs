using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans.Hosting;
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

    private class SiloConfigurator : ISiloConfigurator
    {
        private readonly bool UsePostgreSql = false;
        private readonly bool LogRecords = false;

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
             });

            if (UsePostgreSql)
            {
                hostBuilder.AddAdoNetGrainStorage("OrleansStorage", options =>
                 {
                     options.Invariant = "Npgsql";
                     options.ConnectionString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=password";
                 });
                if(LogRecords)
                    hostBuilder.Services.AddSingleton<IPersistence,PostgreSQLPersistence>();
            } else
            {
                hostBuilder.AddMemoryGrainStorage(Constants.OrleansStorage);
                hostBuilder.Services.AddSingleton<IPersistence, EtcNullPersistence>();
            }

        }
    }

    private class ClientConfigurator : IClientBuilderConfigurator
    {
      public void Configure(IConfiguration configuration, IClientBuilder clientBuilder) => 
        clientBuilder
            .Services.AddSerializer(ser =>
             {
                 ser.AddNewtonsoftJsonSerializer(isSupported: type => type.Namespace.StartsWith("Common"));
             });
    }

    public ClusterFixture()
    {
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<SiloConfigurator>();
        builder.AddClientBuilderConfigurator<ClientConfigurator>();
        Cluster = builder.Build();
        Cluster.Deploy();
    }

    public void Dispose()
    {
        Cluster.StopAllSilos();
    }

    public TestCluster Cluster { get; private set; }
}

