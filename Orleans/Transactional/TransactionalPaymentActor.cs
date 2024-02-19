using Common.Config;
using Microsoft.Extensions.Logging;
using OrleansApp.Abstract;
using OrleansApp.Grains;
using OrleansApp.Infra;
using OrleansApp.Interfaces;

namespace OrleansApp.Transactional;

public class TransactionalPaymentActor : AbstractPaymentActor, ITransactionalPaymentActor
{
    public TransactionalPaymentActor(IPersistence persistence, AppConfig options, ILogger<PaymentActor> _logger) : base(persistence, options, _logger)
    {
    }

    protected override IOrderActor GetOrderActor(int id)
    {
        return GrainFactory.GetGrain<ITransactionalOrderActor>(id);
    }

    protected override IShipmentActor GetShipmentActor(int id)
    {
        return GrainFactory.GetGrain<ITransactionalShipmentActor>(id);
    }

    protected override IStockActor GetStockActor(int sellerId, string productId)
    {
        return GrainFactory.GetGrain<ITransactionalStockActor>(sellerId, productId);
    }
}

