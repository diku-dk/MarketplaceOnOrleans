using Orleans.Configuration;
using OrleansApp.Infra;
using Orleans.Serialization;
using OrleansApp.Infra.SellerDb;
using OrleansApp.Infra.Redis;
using Microsoft.EntityFrameworkCore;
using Common.Config;
using OrleansApp.Service;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

IConfigurationSection configSection = builder.Configuration.GetSection("AppConfig");

var clusterId = configSection.GetValue<string>("Cluster:ClusterId");
var serviceId = configSection.GetValue<string>("Cluster:ServiceId");
var primary = configSection.GetValue<bool>("Cluster:Primary");
var primarySiloIpAddress = configSection.GetValue<string>("Cluster:PrimarySiloIpAddress");

var orleansTransactions = configSection.GetValue<bool>("OrleansTransactions");
var sellerViewPostgres = configSection.GetValue<bool>("SellerViewPostgres");
var shipmentUpdatePostgres = configSection.GetValue<bool>("ShipmentUpdatePostgres");

var orleansStorage = configSection.GetValue<bool>("OrleansStorage");
var adoNetGrainStorage = configSection.GetValue<bool>("AdoNetGrainStorage");
var adoNetConnectionString = configSection.GetValue<string>("AdoNetConnectionString");
var logRecords = configSection.GetValue<bool>("LogRecords");
var feedbackEvents = configSection.GetValue<bool>("FeedbackEvents");
int numShipmentActors = configSection.GetValue<int>("NumShipmentActors");
var useDash = configSection.GetValue<bool>("UseDashboard");
var useSwagger = configSection.GetValue<bool>("UseSwagger");

var streamReplication = configSection.GetValue<bool>("StreamReplication");
var redisReplication = configSection.GetValue<bool>("RedisReplication");
var redisPrimaryConnectionString = configSection.GetValue<string>("RedisPrimaryConnectionString");
var redisSecondaryConnectionString = configSection.GetValue<string>("RedisSecondaryConnectionString");

var trackCartHistory = configSection.GetValue<bool>("TrackCartHistory");

AppConfig appConfig = new()
{
    Cluster = new Cluster
    {
        ClusterId = clusterId,
        ServiceId = serviceId,
        Primary = primary,
        PrimarySiloIpAddress = primarySiloIpAddress
    },
    OrleansTransactions = orleansTransactions,
    SellerViewPostgres = sellerViewPostgres,
    ShipmentUpdatePostgres = shipmentUpdatePostgres,
    StreamReplication = streamReplication,
    RedisReplication = redisReplication,
    RedisPrimaryConnectionString = redisPrimaryConnectionString,
    RedisSecondaryConnectionString = redisSecondaryConnectionString,
    OrleansStorage = orleansStorage,
    AdoNetGrainStorage = adoNetGrainStorage,
    AdoNetConnectionString = adoNetConnectionString,
    LogRecords = logRecords,
    FeedbackEvents = feedbackEvents,
    NumShipmentActors = numShipmentActors,
    UseDashboard = useDash,
    UseSwagger = useSwagger,
    TrackCartHistory = trackCartHistory
};

// Orleans testing has no support for IOptions apparently...
// builder.Services.Configure<AppConfig>(configSection);
builder.Services.AddSingleton(appConfig);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
if(useSwagger){
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

if (logRecords){
    builder.Services.AddSingleton<IAuditLogger, PostgresAuditLogger>();
} else {
    builder.Services.AddSingleton<IAuditLogger, EtcNullPersistence>();
}

// in case aspnet core with orleans client: https://learn.microsoft.com/en-us/dotnet/orleans/tutorials-and-samples/tutorial-1
builder.Host.UseOrleans(siloBuilder =>
{
    if(appConfig.Cluster.ClusterId.SequenceEqual("")){
        siloBuilder
             .UseLocalhostClustering()
             .ConfigureLogging(logging =>
             {
                 logging.ClearProviders();
                 logging.AddConsole();
                 // to change minimum log level, use the following option:
                 //logging.SetMinimumLevel(LogLevel.Warning);
             });
    } else {
        var ipAddress = IPAddress.Parse(appConfig.Cluster.PrimarySiloIpAddress);
        var primarySiloEndpoint = new IPEndPoint(
            ipAddress, // IPAddress.Loopback,
            EndpointOptions.DEFAULT_SILO_PORT);
        if (appConfig.Cluster.Primary)
        {
            siloBuilder.UseDevelopmentClustering(options =>
                {
                    options.PrimarySiloEndpoint = primarySiloEndpoint;
                })
                .Configure<ClusterOptions>(options => {
                    options.ClusterId = appConfig.Cluster.ClusterId;
                    options.ServiceId = appConfig.Cluster.ServiceId;
                })
                .Configure<EndpointOptions>(options => options.AdvertisedIPAddress = ipAddress)
                .ConfigureLogging(logging => logging.AddConsole());
        } else
        {
            siloBuilder.UseDevelopmentClustering(options =>
                {
                    options.PrimarySiloEndpoint = primarySiloEndpoint;
                })
                .Configure<ClusterOptions>(options => {
                    options.ClusterId = appConfig.Cluster.ClusterId;
                    options.ServiceId = appConfig.Cluster.ServiceId;
                })
                .ConfigureEndpoints(siloPort: EndpointOptions.DEFAULT_SILO_PORT, gatewayPort: EndpointOptions.DEFAULT_GATEWAY_PORT)
                .ConfigureLogging(logging => logging.AddConsole());
        }
    }

    if (orleansTransactions)
    {
        siloBuilder.UseTransactions();
        siloBuilder.Configure<ClientMessagingOptions>(options=>{
            //options.ResponseTimeout = TimeSpan.FromMinutes(1);
            options.ResponseTimeoutWithDebugger = TimeSpan.FromMinutes(10);
           //options.DropExpiredMessages = true;
        });
        siloBuilder.Configure<SiloMessagingOptions>(options=>{
            // options.ResponseTimeout = TimeSpan.FromMinutes(1);
            options.ResponseTimeoutWithDebugger = TimeSpan.FromMinutes(10);
            //options.DropExpiredMessages = true;
        });

        siloBuilder.Configure<TransactionalStateOptions>(options => {
            //options.LockAcquireTimeout = TimeSpan.FromMinutes(1);
            //options.LockTimeout = TimeSpan.FromMilliseconds(10000);
            //options.MaxLockGroupSize = 100;
        });
        siloBuilder.Services.AddSerializer(ser => { ser.AddNewtonsoftJsonSerializer(isSupported: type => type.Namespace.StartsWith("Common") || type.Namespace.StartsWith("OrleansApp")); });
    
        if (adoNetGrainStorage)
        {
            siloBuilder.AddAdoNetGrainStorage(Constants.OrleansStorage, options =>
             {
                 options.Invariant = "Npgsql";
                 options.ConnectionString = adoNetConnectionString;
             });
        }
        else
        {
            siloBuilder.AddMemoryGrainStorage(Constants.OrleansStorage);
        }
    } else
    {
        siloBuilder.Services.AddSerializer(ser => ser.AddNewtonsoftJsonSerializer(isSupported: type => type.Namespace.StartsWith("Common")));

        // required to make persistentState being injected on non-transactional grains
        // TODO separate OrleansStorage option from actual writes to storage grain state (WriteAsync)
        siloBuilder.AddMemoryGrainStorage(Constants.OrleansStorage);
    }

    if (sellerViewPostgres)
    {
        siloBuilder.Services.AddDbContextFactory<SellerDbContext>();
        if(shipmentUpdatePostgres)
            builder.Services.AddSingleton<IShipmentService, CustomShipmentServiceImpl>();
        else
            builder.Services.AddSingleton<IShipmentService, DefaultShipmentServiceImpl>();
    } else
    {
        builder.Services.AddSingleton<IShipmentService, DefaultShipmentServiceImpl>();
    }

    if (streamReplication)
    {
        siloBuilder.AddMemoryStreams(Constants.DefaultStreamProvider)
                    .AddMemoryGrainStorage(Constants.DefaultStreamStorage);
    }

    if(useDash){
      siloBuilder.UseDashboard(x => x.HostSelf = true);
    }

    if (redisReplication)
    {
        siloBuilder.Services.AddSingleton<IRedisConnectionFactory>(new RedisConnectionFactoryImpl(redisPrimaryConnectionString, redisSecondaryConnectionString));
    } else
    {
        // just to avoid errors on new instances of TransactionalProductActor
        siloBuilder.Services.AddSingleton<IRedisConnectionFactory>(new EtcNullConnectionFactoryImpl());
    }

    siloBuilder.Services.Configure<ClusterMembershipOptions>(options =>
    {
        options.DefunctSiloCleanupPeriod = TimeSpan.MaxValue;
    });
});

var app = builder.Build();

if (sellerViewPostgres)
{
    AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<SellerDbContext>();
        context.Database.Migrate();

        // truncate order entries on starting a new experiment
        context.OrderEntries.ExecuteDelete();
    }
}

if (logRecords){
    var persistence = app.Services.GetService<IAuditLogger>();
    // init log table in PostgreSQL
    await persistence.SetUpLog();
    await persistence.CleanLog();
    // it guarantees that, upon activating the actor, the state is null
    await persistence.TruncateStorage();
}

// Configure the HTTP request pipeline.
if (useSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
if(useDash) app.Map("/dashboard", x => x.UseOrleansDashboard());

app.MapControllers();

await app.StartAsync();

Console.WriteLine("\n *************************    Configuration    ***************************");
Console.WriteLine(
    " \n ClusterId: "+ appConfig.Cluster.ClusterId +
    " \n ServiceId: "+ appConfig.Cluster.ServiceId +
    " \n Primary: "+ appConfig.Cluster.Primary +
    " \n PrimarySiloIpAddress: "+ appConfig.Cluster.PrimarySiloIpAddress +
    " \n OrleansTransactions: "+ appConfig.OrleansTransactions +  
    " \n SellerViewPostgres: " + appConfig.SellerViewPostgres +
    " \n ShipmentUpdatePostgres: " + appConfig.ShipmentUpdatePostgres +
    " \n OrleansStorage: " + appConfig.OrleansStorage +
    " \n AdoNetGrainStorage: "+appConfig.AdoNetGrainStorage +
    " \n AdoNetConnectionString: "+appConfig.AdoNetConnectionString +
    " \n LogRecords: "+appConfig.LogRecords +
    " \n FeedbackEvents: "+appConfig.FeedbackEvents +
    " \n UseSwagger: "+useSwagger +
    " \n UseDashboard: "+appConfig.UseDashboard +
    " \n NumShipmentActors: "+appConfig.NumShipmentActors +
    " \n Stream Replication: " + appConfig.StreamReplication +
    " \n RedisReplication: " + appConfig.RedisReplication +
    " \n RedisPrimaryConnectionString: "+ appConfig.RedisPrimaryConnectionString +
    " \n RedisSecondaryConnectionString: "+ appConfig.RedisSecondaryConnectionString +
    " \n TrackCartHistory: "+appConfig.TrackCartHistory
    );

Console.WriteLine("\n *************************************************************************");
Console.WriteLine("            The Orleans server started. Press any key to terminate...         ");
Console.WriteLine("\n *************************************************************************");

Console.ReadLine();

await app.StopAsync();