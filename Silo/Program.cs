using Common;
using Orleans.Infra;
using Orleans.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

IConfigurationSection configSection = builder.Configuration.GetSection("AppConfig");

if (configSection.GetValue<bool>("CleanLogFilesOnStart"))
{
    Helper.CleanLogFiles();
}

var useTx = configSection.GetValue<bool>("UseTransactions");
// only if an actor needs this injection
builder.Services.Configure<AppConfig>(configSection);

builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// in case aspnet core with orleans client: https://learn.microsoft.com/en-us/dotnet/orleans/tutorials-and-samples/tutorial-1
builder.Host.UseOrleans(siloBuilder =>
{
    if (useTx)
    {
        siloBuilder.UseTransactions();
    }
    siloBuilder
         .UseLocalhostClustering()
         .AddAdoNetGrainStorage(Constants.OrleansStorage, options =>
         {
             options.Invariant = "Npgsql";
             options.ConnectionString = Constants.postgresConnectionString;
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
         }).Configure<AppConfig>(configSection);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
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

if (configSection.GetValue<bool>("CleanLogFilesOnStop"))
{
    Console.WriteLine("\n *************************** Deleting log files... ***************************");
    Helper.CleanLogFiles();
}
