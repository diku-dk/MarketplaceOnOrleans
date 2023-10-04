using System.Reflection;
using Common;
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
    private const string PostgresConnectionString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=password;Pooling=true;Minimum Pool Size=0;Maximum Pool Size=10000";

    public static bool UsePostgreSql = false;
    public static bool LogRecords = false;

    public static int NumShipmentActors = 1;

    public static Dictionary<string, string> myConfiguration = new Dictionary<string, string>
                                        {
                                            {"NumShipmentActors", NumShipmentActors.ToString()}
                                        };

    private class SiloConfigurator : ISiloConfigurator
    {
        // https://stackoverflow.com/questions/55497800/populate-iconfiguration-for-unit-tests
        public void Configure(ISiloBuilder hostBuilder) {

            var configuration = new ConfigurationBuilder()
                                    .AddInMemoryCollection(myConfiguration)
                                    .Build();
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
             }).Configure<AppConfig>(configuration);
            
            if (UsePostgreSql)
            {
                hostBuilder.AddAdoNetGrainStorage(Constants.OrleansStorage, options =>
                 {
                     options.Invariant = "Npgsql";
                     options.ConnectionString = PostgresConnectionString;
                 });
                if(LogRecords)
                    hostBuilder.Services.AddSingleton<IPersistence, PostgreSQLPersistence>();
            } else
            {
                hostBuilder.AddMemoryGrainStorage(Constants.OrleansStorage);
                if(LogRecords)
                    hostBuilder.Services.AddSingleton<IPersistence, PostgreSQLPersistence>();
                else
                    hostBuilder.Services.AddSingleton<IPersistence, EtcNullPersistence>();
            }

        }
    }

    private class ClientConfigurator : IClientBuilderConfigurator
    {
      public void Configure(IConfiguration configuration, IClientBuilder clientBuilder) {
        clientBuilder
            .Services.AddSerializer(ser =>
             {
                 ser.AddNewtonsoftJsonSerializer(isSupported: type => type.Namespace.StartsWith("Common"));
             });
            if(LogRecords)
                clientBuilder.Services.AddSingleton<IPersistence,PostgreSQLPersistence>();
            else
                clientBuilder.Services.AddSingleton<IPersistence, EtcNullPersistence>();
        }
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

