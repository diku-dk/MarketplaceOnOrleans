using Common.Config;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;
using OrleansApp.Abstract;
using OrleansApp.Grains;
using OrleansApp.Infra;
using OrleansApp.Interfaces;

namespace OrleansApp.Transactional;

[Reentrant]
public sealed class TransactionalPaymentActor : AbstractPaymentActor, ITransactionalPaymentActor
{
    public TransactionalPaymentActor(IAuditLogger persistence, AppConfig options, ILogger<PaymentActor> _logger) : base(persistence, options, _logger)
    {
    }

    protected override ITransactionalOrderActor GetOrderActor(int id)
    {
        return GrainFactory.GetGrain<ITransactionalOrderActor>(id);
    }

    protected override ITransactionalShipmentActor GetShipmentActor(int id)
    {
        return GrainFactory.GetGrain<ITransactionalShipmentActor>(id);
    }

    protected override ITransactionalStockActor GetStockActor(int sellerId, string productId)
    {
        return GrainFactory.GetGrain<ITransactionalStockActor>(sellerId, productId);
    }
}

