using Common.Config;
using Common.Entities;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;
using Orleans.Transactions.Abstractions;
using OrleansApp.Abstract;
using OrleansApp.Infra;

namespace OrleansApp.Transactional;

/**
* For some unknown reason, having reentrancy here leads to non-deterministic degradation of performance.
* News: From Orleans 8.0 on, [Reentrant] is necessary in every transactional grain
*/
[Reentrant]
public sealed class TransactionalPaymentActor : AbstractPaymentActor, ITransactionalPaymentActor
{

    private readonly ITransactionalState<SortedDictionary<int,List<OrderPayment>>> orderPayments;
    private readonly ITransactionalState<SortedDictionary<int,OrderPaymentCard>> orderPaymentCards;

    public TransactionalPaymentActor(
         [TransactionalState(stateName: "orderPayments", storageName: Constants.OrleansStorage)] ITransactionalState<SortedDictionary<int,List<OrderPayment>>> orderPayments,
         [TransactionalState(stateName: "orderPaymentCards", storageName: Constants.OrleansStorage)] ITransactionalState<SortedDictionary<int,OrderPaymentCard>> orderPaymentCards,
         IAuditLogger persistence, 
         AppConfig options, 
         ILogger<TransactionalPaymentActor> logger) : base(persistence, options, logger)
    {
        this.orderPayments = orderPayments;
        this.orderPaymentCards = orderPaymentCards;
    }

    public override Task InsertPaymentIntoState(int id, OrderPaymentCard orderPaymentCard, List<OrderPayment> orderPayments)
    {
        Task t1 = this.orderPayments.PerformUpdate(s => { s.Add(id, orderPayments); });
        Task t2 = this.orderPaymentCards.PerformUpdate(p => { p.Add(id, orderPaymentCard); });
        return Task.WhenAll(t1, t2);
    }

    public override Task Reset()
    {
        Task t1 = this.orderPayments.PerformUpdate(s => { s.Clear(); });
        Task t2 = this.orderPaymentCards.PerformUpdate(p => { p.Clear(); });
        return Task.WhenAll(t1, t2);
    }

}

