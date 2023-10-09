using Common;
using Microsoft.Extensions.Logging;
using Orleans.Abstract;
using Orleans.Grains;
using Orleans.Infra;
using Orleans.Interfaces;

namespace Orleans.Transactional;

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

