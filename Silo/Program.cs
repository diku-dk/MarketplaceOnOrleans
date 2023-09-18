using Orleans.Infra;
using Orleans.Serialization;

var builder = WebApplication.CreateBuilder(args);

// init log table in PostgreSQL
Helper.SetUpLog();

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
             options.ConnectionString = Constants.PostgresConnectionString;
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
         .AddSingleton<IPersistence,PostgreSQLPersistence>();;
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