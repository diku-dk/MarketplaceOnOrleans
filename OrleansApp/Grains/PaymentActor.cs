using Microsoft.Extensions.Logging;
using OrleansApp.Abstract;
using Orleans.Concurrency;
using OrleansApp.Infra;
using Common.Config;
using Common.Entities;

namespace OrleansApp.Grains;

[Reentrant]
public sealed class PaymentActor : AbstractPaymentActor
{
    private readonly IPersistentState<SortedDictionary<int,List<OrderPayment>>> orderPayments;
    private readonly IPersistentState<SortedDictionary<int,OrderPaymentCard>> orderPaymentCards;

    public PaymentActor(
         [PersistentState(stateName: "orderPayments", storageName: Constants.OrleansStorage)] IPersistentState<SortedDictionary<int,List<OrderPayment>>> orderPayments,
         [PersistentState(stateName: "orderPaymentCards", storageName: Constants.OrleansStorage)] IPersistentState<SortedDictionary<int,OrderPaymentCard>> orderPaymentCards,
         IAuditLogger persistence,
         AppConfig options,
         ILogger<PaymentActor> logger) : base(persistence, options, logger)
	{
        this.orderPayments = orderPayments;
        this.orderPaymentCards = orderPaymentCards;
    }

    public override async Task Reset()
    {
        this.orderPayments.State.Clear();
        this.orderPaymentCards.State.Clear();
        if (this.config.OrleansStorage)
        {
            await Task.WhenAll(this.orderPayments.WriteStateAsync(), this.orderPaymentCards.WriteStateAsync());
        }
    }

    public override async Task InsertPaymentIntoState(int id, OrderPaymentCard orderPaymentCard, List<OrderPayment> orderPayments)
    {
        try
        {
            this.orderPayments.State.Add(id, orderPayments);
            this.orderPaymentCards.State.Add(id, orderPaymentCard);
        }
        catch (Exception e)
        {
            var str = string.Format("Payment (for customer ID {0}) processing unique ID {1}. Error: {2}", this.customerId, id, e.Message);
            this.logger.LogError(str);
            throw new InvalidOperationException(str);
        }
        if (this.config.OrleansStorage)
        {
            await Task.WhenAll(this.orderPayments.WriteStateAsync(), this.orderPaymentCards.WriteStateAsync());
        }
    }

}