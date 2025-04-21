using Common.Entities;
using Common.Events;
using Common.Integration;
using Microsoft.Extensions.Logging;
using OrleansApp.Grains;
using OrleansApp.Infra;
using OrleansApp.Interfaces;
using OrleansApp.Interfaces.SellerView;
using System.Text;
using System.Text.Json;
using Common.Config;
using OrleansApp.Transactional;

namespace OrleansApp.Abstract;

public abstract class AbstractPaymentActor : Grain, IPaymentActor
{
    private static readonly string Name = typeof(PaymentActor).FullName;
    protected readonly AppConfig config;
    protected int customerId;
    protected readonly ILogger<IPaymentActor> logger;
    private readonly IAuditLogger persistence;

    private delegate ISellerActor GetSellerActorDelegate(int sellerId);
    private readonly GetSellerActorDelegate getSellerDelegate;

    private class PaymentState
    {
        public List<OrderPayment> orderPayments { get; set; }
        public OrderPaymentCard orderPaymentCard { get; set; }

        public PaymentState() { }
    }

    public AbstractPaymentActor(IAuditLogger persistence, AppConfig options, ILogger<IPaymentActor> _logger)
    {
        this.persistence = persistence;
        this.config = options;
        this.logger = _logger;
        this.getSellerDelegate = config.SellerViewPostgres ? GetSellerViewActor : GetSellerActor;
    }

    public override Task OnActivateAsync(CancellationToken token)
    {
        this.customerId = (int)this.GetPrimaryKeyLong();
        return Task.CompletedTask;
    }

    public abstract Task Reset();

    public abstract Task InsertPaymentIntoState(int id, OrderPaymentCard orderPaymentCard, List<OrderPayment> orderPayments);

    private static bool IsCard(string type){
        switch (type){
            case "CREDIT_CARD":{
                return true;
            }
            case "DEBIT_CARD":{
                return true;
            }
            default: {
                return false;
            }
        }
    }

    private PaymentStatus GetPaymentStatus(InvoiceIssued invoiceIssued) {
        PaymentStatus status;
        if(this.config.PaymentProvider){
            // TODO provider communication
            status = PaymentStatus.requires_payment_method;
        } else {
            status = PaymentStatus.succeeded;
        }
        return status;
    }

    public async Task ProcessPayment(InvoiceIssued invoiceIssued)
    {
        this.logger.LogDebug("APP: Payment received an invoice issued event with TID: "+ invoiceIssued.instanceId);
        var paymentTs = DateTime.UtcNow;
        PaymentStatus status = GetPaymentStatus(invoiceIssued);
        int seq = 1;
        var isCard = IsCard(invoiceIssued.customer.PaymentType);

        var orderPayments = new List<OrderPayment>();
        OrderPaymentCard orderPaymentCard = null;
        if (isCard)
        {
            var cardPaymentLine = new OrderPayment()
            {
                order_id = invoiceIssued.orderId,
                payment_sequential = seq,
                type = invoiceIssued.customer.PaymentType.SequenceEqual(PaymentType.CREDIT_CARD.ToString()) ?
                    PaymentType.CREDIT_CARD : PaymentType.DEBIT_CARD,
                installments = invoiceIssued.customer.Installments,
                value = invoiceIssued.totalInvoice,
                status = status
            };
            orderPayments.Add(cardPaymentLine);

            // create an entity for credit card payment details with FK to order payment
            orderPaymentCard = new OrderPaymentCard()
            {
                order_id = invoiceIssued.orderId,
                payment_sequential = seq,
                card_number = invoiceIssued.customer.CardNumber,
                card_holder_name = invoiceIssued.customer.CardHolderName,
                card_expiration = invoiceIssued.customer.CardExpiration,
                card_brand = invoiceIssued.customer.CardBrand
            };
            seq++;
        }

        if (invoiceIssued.customer.PaymentType.SequenceEqual(PaymentType.BOLETO.ToString()))
        {
            orderPayments.Add(new OrderPayment()
            {
                order_id = invoiceIssued.orderId,
                payment_sequential = seq,
                type = PaymentType.BOLETO,
                installments = 1,
                value = invoiceIssued.totalInvoice,
                status = status
            });
            seq++;
        }

        // then one line for each voucher
        if(status == PaymentStatus.succeeded){
            foreach (var item in invoiceIssued.items)
            {
                if (item.voucher > 0)
                {
                    orderPayments.Add(new OrderPayment()
                    {
                        order_id = invoiceIssued.orderId,
                        payment_sequential = seq,
                        type = PaymentType.VOUCHER,
                        installments = 1,
                        value = item.voucher
                    });

                    seq++;
                }
            }
        }

        await this.InsertPaymentIntoState(invoiceIssued.orderId, orderPaymentCard, orderPayments);

        var tasks = new List<Task>();

        // Using strings below, but can also use byte arrays for both keys and values
        if (this.config.LogRecords)
        {
            var str = JsonSerializer.Serialize(new PaymentState() { orderPayments = orderPayments, orderPaymentCard = orderPaymentCard });
            var key = new StringBuilder(this.customerId.ToString()).Append('-').Append(invoiceIssued.orderId).ToString();
            tasks.Add(persistence.Log(Name, key, str));
        }

        var paymentConfirmedWithItems = new PaymentConfirmed(invoiceIssued.customer, invoiceIssued.orderId, invoiceIssued.totalInvoice, invoiceIssued.items, paymentTs, invoiceIssued.instanceId);

        if(this.config.FeedbackEvents){
            // inform related stock actors to reduce the amount because the payment has succeeded
            foreach (var item in invoiceIssued.items)
            {
                Task taskStock;
                if (this.config.OrleansTransactions)
                {
                    var stockActor = GetTxStockActor(item.seller_id, item.product_id.ToString());
                    taskStock = stockActor.ConfirmReservation(item.quantity);
                } else
                {
                    var stockActor = GetDefaultStockActor(item.seller_id, item.product_id.ToString());
                    taskStock = stockActor.ConfirmReservation(item.quantity);
                }
               tasks.Add(taskStock);
            }

            var sellers = invoiceIssued.items.Select(x => x.seller_id).ToHashSet();
            foreach (var sellerID in sellers)
            {
                var sellerActor = this.getSellerDelegate(sellerID);
                tasks.Add(sellerActor.ProcessPaymentConfirmed(paymentConfirmedWithItems));
            }

            var paymentConfirmedNoItems = new PaymentConfirmed(invoiceIssued.customer, invoiceIssued.orderId, invoiceIssued.totalInvoice, null, paymentTs, invoiceIssued.instanceId);

            Task taskOrder;
            if (this.config.OrleansTransactions)
            {
                taskOrder = this.GetTxOrderActor(this.customerId).ProcessPaymentConfirmed(paymentConfirmedNoItems);
            }
            else
            {
                taskOrder = this.GetDefaultOrderActor(this.customerId).ProcessPaymentConfirmed(paymentConfirmedNoItems);
            }
            
            tasks.Add(GrainFactory.GetGrain<ICustomerActor>(this.customerId).NotifyPaymentConfirmed(paymentConfirmedNoItems));
            tasks.Add(taskOrder);
            await Task.WhenAll(tasks);
        }

        var shipmentActorID = Helper.GetShipmentActorID(this.customerId, this.config.NumShipmentActors);
        if(this.config.OrleansTransactions){
            var shipmentActor = this.GetTxShipmentActor(shipmentActorID);
            await shipmentActor.ProcessShipment(paymentConfirmedWithItems);
        }
        else
        {
            var shipmentActor = this.GetDefaultShipmentActor(shipmentActorID);
            await shipmentActor.ProcessShipment(paymentConfirmedWithItems);
        }
    }

    private ISellerActor GetSellerActor(int sellerId)
    {
        return this.GrainFactory.GetGrain<ISellerActor>(sellerId);
    }

    private ISellerViewActor GetSellerViewActor(int sellerId)
    {
        return this.GrainFactory.GetGrain<ISellerViewActor>(sellerId);
    }

    protected IOrderActor GetDefaultOrderActor(int id)
    {
        return GrainFactory.GetGrain<IOrderActor>(id);
    }

    protected IShipmentActor GetDefaultShipmentActor(int id)
    {
        return GrainFactory.GetGrain<IShipmentActor>(id);
    }

    protected IStockActor GetDefaultStockActor(int sellerId, string productId)
    {
        return GrainFactory.GetGrain<IStockActor>(sellerId, productId);
    }

    protected ITransactionalOrderActor GetTxOrderActor(int id)
    {
        return GrainFactory.GetGrain<ITransactionalOrderActor>(id);
    }

    protected ITransactionalShipmentActor GetTxShipmentActor(int id)
    {
        return GrainFactory.GetGrain<ITransactionalShipmentActor>(id);
    }

    protected ITransactionalStockActor GetTxStockActor(int sellerId, string productId)
    {
        return GrainFactory.GetGrain<ITransactionalStockActor>(sellerId, productId);
    }
}

