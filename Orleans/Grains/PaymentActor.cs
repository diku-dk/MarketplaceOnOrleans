using Common.Config;
using Microsoft.Extensions.Logging;
using OrleansApp.Abstract;
using Orleans.Concurrency;
using OrleansApp.Infra;
using OrleansApp.Interfaces;

namespace OrleansApp.Grains;

[Reentrant]
public sealed class PaymentActor : AbstractPaymentActor
{
    public PaymentActor(IPersistence persistence, AppConfig options, ILogger<PaymentActor> _logger) : base(persistence, options, _logger)
    {
    }

    protected override IOrderActor GetOrderActor(int id)
    {
        return GrainFactory.GetGrain<IOrderActor>(id);
    }

    protected override IShipmentActor GetShipmentActor(int id)
    {
        return GrainFactory.GetGrain<IShipmentActor>(id);
    }

    protected override IStockActor GetStockActor(int sellerId, string productId)
    {
        return GrainFactory.GetGrain<IStockActor>(sellerId, productId);
    }
}