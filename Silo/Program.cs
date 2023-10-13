using Common;
using Orleans.Configuration;
using OrleansApp.Infra;
using Orleans.Serialization;

var builder = WebApplication.CreateBuilder(args);

IConfigurationSection configSection = builder.Configuration.GetSection("AppConfig");

var orleansTransactions = configSection.GetValue<bool>("OrleansTransactions");
var orleansStorage = configSection.GetValue<bool>("OrleansStorage");
var adoNetGrainStorage = configSection.GetValue<bool>("AdoNetGrainStorage");
var connectionString = configSection.GetValue<string>("ConnectionString");
var logRecords = configSection.GetValue<bool>("LogRecords");
int numShipmentActors = configSection.GetValue<int>("NumShipmentActors");
var useDash = configSection.GetValue<bool>("UseDashboard");
var useSwagger = configSection.GetValue<bool>("UseSwagger");

AppConfig appConfig = new()
{
     OrleansTransactions = orleansTransactions,
     OrleansStorage = orleansStorage,
     AdoNetGrainStorage = adoNetGrainStorage,
     ConnectionString = connectionString,
     LogRecords = logRecords,
     NumShipmentActors = numShipmentActors,
     UseDashboard = useDash,
     UseSwagger = useSwagger
};

bool usePostgreSQL = orleansStorage && adoNetGrainStorage;

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
    builder.Services.AddSingleton<IPersistence, PostgreSQLPersistence>();
} else {
    builder.Services.AddSingleton<IPersistence, EtcNullPersistence>();
}

// in case aspnet core with orleans client: https://learn.microsoft.com/en-us/dotnet/orleans/tutorials-and-samples/tutorial-1
builder.Host.UseOrleans(siloBuilder =>
{
    siloBuilder
         .UseLocalhostClustering()
         .ConfigureLogging(logging =>
         {
             logging.ClearProviders();
             logging.AddConsole();
             //logging.SetMinimumLevel(LogLevel.Warning);
         });

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

        // TransactionalStateOptions
        siloBuilder.Services.AddSerializer(ser => { ser.AddNewtonsoftJsonSerializer(isSupported: type => type.Namespace.StartsWith("Common") || type.Namespace.StartsWith("OrleansApp")); });
    } else
    {
        siloBuilder.Services.AddSerializer(ser => ser.AddNewtonsoftJsonSerializer(isSupported: type => type.Namespace.StartsWith("Common")));
    }
         
    if (usePostgreSQL){
        siloBuilder.AddAdoNetGrainStorage(Constants.OrleansStorage, options =>
         {
             options.Invariant = "Npgsql";
             options.ConnectionString = connectionString;
         });
    }
    else
    {
        siloBuilder.AddMemoryGrainStorage(Constants.OrleansStorage);
    }
    if (logRecords){
        siloBuilder.Services.AddSingleton<IPersistence, PostgreSQLPersistence>();
    } else {
        siloBuilder.Services.AddSingleton<IPersistence, EtcNullPersistence>();
    }
    if(useDash){
      siloBuilder.UseDashboard(x => x.HostSelf = true);
    }
});

var app = builder.Build();

if (logRecords){
    var persistence = app.Services.GetService<IPersistence>();
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

Console.WriteLine("\n *************************************************************************");
Console.WriteLine(" OrleansTransactions: "+ appConfig.OrleansTransactions + " \n OrleansStorage: " + appConfig.OrleansStorage+" \n AdoNetGrainStorage: "+appConfig.AdoNetGrainStorage+" \n Log Records: "+appConfig.LogRecords+" \n Use Swagger: "+useSwagger+" \n UseDashboard: "+appConfig.UseDashboard+" \n NumShipmentActors: "+appConfig.NumShipmentActors+ " ");
Console.WriteLine("            The Orleans server started. Press any key to terminate...         ");
Console.WriteLine("\n *************************************************************************");

Console.ReadLine();

await app.StopAsync();