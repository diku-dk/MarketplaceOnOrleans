using Common;
using Orleans.Infra;
using Orleans.Serialization;

var builder = WebApplication.CreateBuilder(args);

IConfigurationSection configSection = builder.Configuration.GetSection("AppConfig");
builder.Services.Configure<AppConfig>(configSection);

var useDash = configSection.GetValue<bool>("UseDashboard");
var orleansStorage = configSection.GetValue<bool>("OrleansStorage");
var adoNetGrainStorage = configSection.GetValue<bool>("AdoNetGrainStorage");
var logRecord = configSection.GetValue<bool>("LogRecords");
var useSwagger = configSection.GetValue<bool>("UseSwagger");
int numShipmentActors = configSection.GetValue<int>("NumShipmentActors");

bool usePostgreSQL = orleansStorage && adoNetGrainStorage;

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
if(useSwagger){
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

if (usePostgreSQL){
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
             logging.SetMinimumLevel(LogLevel.Warning);
         })
         .Services.AddSerializer(ser =>
         {
             ser.AddNewtonsoftJsonSerializer(isSupported: type => type.Namespace.StartsWith("Common"));
         });
         
    if (usePostgreSQL){
         var connectionString = configSection.GetValue<string>("ConnectionString");
        siloBuilder.AddAdoNetGrainStorage(Constants.OrleansStorage, options =>
         {
             options.Invariant = "Npgsql";
             options.ConnectionString = connectionString;
         });
        siloBuilder.Services.AddSingleton<IPersistence, PostgreSQLPersistence>();
    }
    else
    {
        siloBuilder.AddMemoryGrainStorage(Constants.OrleansStorage);
        siloBuilder.Services.AddSingleton<IPersistence, EtcNullPersistence>();
    }

    if(useDash){
      siloBuilder.UseDashboard(x => x.HostSelf = true);
    }
});

var app = builder.Build();

if (usePostgreSQL){
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
Console.WriteLine(" OrleansStorage: "+orleansStorage+" \n AdoNetGrainStorage: "+adoNetGrainStorage+" \n Log Record: "+logRecord+" \n Use Swagger: "+useSwagger+" \n UseDashboard: "+useDash+" \n NumShipmentActors: "+numShipmentActors+ " ");
Console.WriteLine("            The Orleans server started. Press any key to terminate...         ");
Console.WriteLine("\n *************************************************************************");

Console.ReadLine();

await app.StopAsync();