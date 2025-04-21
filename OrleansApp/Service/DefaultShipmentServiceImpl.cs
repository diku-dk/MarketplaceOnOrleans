using Common.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrleansApp.Interfaces;
using OrleansApp.Transactional;

namespace OrleansApp.Service;

public sealed class DefaultShipmentServiceImpl : IShipmentService
{
    private delegate IShipmentActor GetShipmentActorDelegate(int partitionId);

    private readonly AppConfig config;
    private readonly IGrainFactory grainFactory;
    private readonly ITransactionClient transactionClient;
    private readonly ILogger<DefaultShipmentServiceImpl> logger;

    public DefaultShipmentServiceImpl(AppConfig config, IGrainFactory grainFactory, IHost host, ILogger<DefaultShipmentServiceImpl> logger)
    {
        this.config = config;
        this.grainFactory = grainFactory;
        this.transactionClient = config.OrleansTransactions ? host.Services.GetRequiredService<ITransactionClient>() : null;
        this.logger = logger;
    }

    public async Task UpdateShipment(string instanceId)
    {
        List<Task> tasks = new List<Task>(this.config.NumShipmentActors);
        if (this.config.OrleansTransactions)
        {
            for (int i = 0; i < this.config.NumShipmentActors; i++)
            {
                var grain = this.GetTxShipmentActor(i);
                Task t = this.transactionClient.RunTransaction(TransactionOption.Create, () => grain.UpdateShipment(instanceId));
                tasks.Add(t);
            }
        }
        else
        {
            for (int i = 0; i < this.config.NumShipmentActors; i++)
            {
                var grain = this.GetDefaultShipmentActor(i);
                tasks.Add(grain.UpdateShipment(instanceId));
            }
        }
        await Task.WhenAll(tasks);
    }

    public async Task ResetShipmentActors()
    {
        List<Task> tasks = new List<Task>(config.NumShipmentActors);
        for(int i = 0; i < this.config.NumShipmentActors; i++)
        {
            if (this.config.OrleansTransactions)
            {
                var grain = this.GetTxShipmentActor(i);
                Task t = this.transactionClient.RunTransaction(TransactionOption.Create, grain.Reset);
                tasks.Add(t);
            }
            else {
                var grain = this.GetDefaultShipmentActor(i);
                tasks.Add(grain.Reset());
            }
        }
        await Task.WhenAll(tasks);
        this.logger.LogWarning("{0} shipment states reset", this.config.NumShipmentActors);
    }

    private IShipmentActor GetDefaultShipmentActor(int partitionId)
    {
        return this.grainFactory.GetGrain<IShipmentActor>(partitionId);
    }

    private ITransactionalShipmentActor GetTxShipmentActor(int partitionId)
    {
        return this.grainFactory.GetGrain<ITransactionalShipmentActor>(partitionId);
    }

}

