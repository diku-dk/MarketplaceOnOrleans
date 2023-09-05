using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Orleans.Serialization;
using Orleans.TestingHost;

namespace Test;

/**
* https://learn.microsoft.com/en-us/dotnet/orleans/tutorials-and-samples/testing
* https://stackoverflow.com/questions/70640638/how-to-configure-testclusterbuilder-such-that-the-test-cluster-has-access-to-sms
*/
public class ClusterFixture : IDisposable
{

    private class SiloConfigurator : ISiloConfigurator
    {
      public void Configure(ISiloBuilder hostBuilder) =>
         hostBuilder
         .AddMemoryStreams("SMSProvider")
         .AddMemoryGrainStorage("PubSubStore")
         .AddAdoNetGrainStorage("OrleansStorage", options =>
         {
             
             options.Invariant = "Npgsql";
             options.ConnectionString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=password";
         })
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
        clientBuilder.AddMemoryStreams("SMSProvider")
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

