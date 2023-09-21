using Common;
using Orleans.Infra;
using Orleans.Serialization;

var builder = WebApplication.CreateBuilder(args);

IConfigurationSection configSection = builder.Configuration.GetSection("AppConfig");
builder.Services.Configure<AppConfig>(configSection);
var useDash = configSection.GetValue<bool>("UseDashboard");
var connectionString = configSection.GetValue<string>("ConnectionString");

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IPersistence, PostgreSQLPersistence>();

// in case aspnet core with orleans client: https://learn.microsoft.com/en-us/dotnet/orleans/tutorials-and-samples/tutorial-1
builder.Host.UseOrleans(siloBuilder =>
{
    siloBuilder
         .UseLocalhostClustering()
         .AddAdoNetGrainStorage(Constants.OrleansStorage, options =>
         {
             options.Invariant = "Npgsql";
             options.ConnectionString = connectionString;
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
         })
         .AddSingleton<IPersistence, PostgreSQLPersistence>();

    if(useDash)
      siloBuilder.UseDashboard(x => x.HostSelf = true);
});
var app = builder.Build();

var persistence = app.Services.GetService<IPersistence>();
// init log table in PostgreSQL
// Helper.SetUpLog();
await persistence.SetUpLog();
// it guarantees that, upon activating the actor, the state is null
await persistence.ResetActorStates();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    if(useDash) app.Map("/dashboard", x => x.UseOrleansDashboard());
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

await app.StartAsync();

Console.WriteLine("\n *************************************************************************");
Console.WriteLine("            The Orleans server started. Press any key to terminate...         ");
Console.WriteLine("\n *************************************************************************");

Console.ReadLine();

await app.StopAsync();