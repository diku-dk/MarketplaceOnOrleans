using Orleans.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// in case aspnet core with orleans client: https://learn.microsoft.com/en-us/dotnet/orleans/tutorials-and-samples/tutorial-1
builder.Host.UseOrleans(siloBuilder =>
{
    siloBuilder
         .UseLocalhostClustering()
         .AddMemoryStreams(Orleans.Infra.Constants.DefaultStreamProvider)
         .AddMemoryGrainStorage(Orleans.Infra.Constants.DefaultStreamStorage)
         .AddAdoNetGrainStorage(Orleans.Infra.Constants.OrleansStorage, options =>
         {
             options.Invariant = "Npgsql";
             // options.ConnectionString = "Host=ep-ancient-wildflower-518871.eu-central-1.aws.neon.tech;Port=5432;Database=neondb;Username=rodrigolaigner;Password=uYsWSG1dm2QB";
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