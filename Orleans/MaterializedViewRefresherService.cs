using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SellerMS.Infra;

namespace Orleans;

public class MaterializedViewRefresherService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private Timer _timer;

    public MaterializedViewRefresherService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _timer = new Timer(RefreshMaterializedView, null, TimeSpan.Zero, TimeSpan.FromSeconds(30)); 
        return Task.CompletedTask;
    }

    private void RefreshMaterializedView(object state)
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SellerDbContext>();
            dbContext.Database.ExecuteSqlRaw(SellerDbContext.RefreshMaterializedView);
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        await base.StopAsync(stoppingToken);
    }

    public override void Dispose()
    {
        _timer?.Dispose();
        base.Dispose();
    }
}
