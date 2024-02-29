using Common.Config;
using Microsoft.Extensions.Logging;
using OrleansApp.Interfaces;
using OrleansApp.Transactional;

namespace OrleansApp.Service;

public sealed class DefaultShipmentServiceImpl : IShipmentService
{
    private delegate IShipmentActor GetShipmentActorDelegate(int partitionId);

    private readonly AppConfig config;
    private readonly GetShipmentActorDelegate callback;
    private readonly IGrainFactory grainFactory;
    private readonly ILogger<DefaultShipmentServiceImpl> logger;

    public DefaultShipmentServiceImpl(AppConfig config, IGrainFactory grainFactory, ILogger<DefaultShipmentServiceImpl> logger)
    {
        this.config = config;
        this.callback = config.OrleansTransactions ? GetTransactionalShipmentActor : GetShipmentActor;
        this.grainFactory = grainFactory;
        this.logger = logger;
    }

    public async Task UpdateShipment(string instanceId)
    {
        List<Task> tasks = new List<Task>(config.NumShipmentActors);
        for (int i = 0; i < config.NumShipmentActors; i++)
        {
            var grain = this.callback(i);
            tasks.Add(grain.UpdateShipment(instanceId));
        }
        await Task.WhenAll(tasks);
    }

    private IShipmentActor GetShipmentActor(int partitionId)
    {
        return this.grainFactory.GetGrain<IShipmentActor>(partitionId);
    }

    private ITransactionalShipmentActor GetTransactionalShipmentActor(int partitionId)
    {
        return this.grainFactory.GetGrain<ITransactionalShipmentActor>(partitionId);
    }

}

