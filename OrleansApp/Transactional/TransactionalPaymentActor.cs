using Common.Config;
using Microsoft.Extensions.Logging;
using OrleansApp.Abstract;
using OrleansApp.Grains;
using OrleansApp.Infra;

namespace OrleansApp.Transactional;

/**
* For some unknown reason, having reentrancy here leads to non-deterministic degradation of performance.
*/
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

