﻿using Common.Entities;
using Common.Events;
using Common;
using Microsoft.Extensions.Logging;
using Orleans.Grains;
using Orleans.Infra;
using Orleans.Interfaces;
using System.Text;
using System.Text.Json;

namespace Orleans.Abstract;

public abstract class AbstractPaymentActor : Grain, IPaymentActor
{
    private static readonly string Name = typeof(PaymentActor).FullName;
    private readonly AppConfig config;
    private int customerId;
    private readonly ILogger<PaymentActor> logger;
    private readonly IPersistence persistence;

    private class PaymentState
    {
        public List<OrderPayment> orderPayments { get; set; }
        public OrderPaymentCard card { get; set; }

        public PaymentState() { }
    }

    public AbstractPaymentActor(IPersistence persistence, AppConfig options, ILogger<PaymentActor> _logger)
    {
        this.persistence = persistence;
        this.config = options;
        this.logger = _logger;
    }

    public override Task OnActivateAsync(CancellationToken token)
    {
        this.customerId = (int)this.GetPrimaryKeyLong();
        return Task.CompletedTask;
    }

    public async Task ProcessPayment(InvoiceIssued invoiceIssued)
    {
        int seq = 1;

        var cc = invoiceIssued.customer.PaymentType.Equals(PaymentType.CREDIT_CARD.ToString());

        // create payment tuples
        var orderPayments = new List<OrderPayment>();
        OrderPaymentCard card = null;
        if (cc || invoiceIssued.customer.PaymentType.Equals(PaymentType.DEBIT_CARD.ToString()))
        {
            var cardPaymentLine = new OrderPayment()
            {
                order_id = invoiceIssued.orderId,
                payment_sequential = seq,
                type = cc ? PaymentType.CREDIT_CARD : PaymentType.DEBIT_CARD,
                installments = invoiceIssued.customer.Installments,
                value = invoiceIssued.totalInvoice,
                status = Common.Integration.PaymentStatus.succeeded
            };
            orderPayments.Add(cardPaymentLine);

            // create an entity for credit card payment details with FK to order payment
            card = new OrderPaymentCard()
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
                status = Common.Integration.PaymentStatus.succeeded
            });

            seq++;
        }

        // then one line for each voucher
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

        var tasks = new List<Task>();

        // Using strings below, but can also use byte arrays for both keys and values
        if (config.LogRecords)
        {
            var str = JsonSerializer.Serialize(new PaymentState() { orderPayments = orderPayments, card = card });
            var key = new StringBuilder(invoiceIssued.customer.CustomerId.ToString()).Append('-').Append(invoiceIssued.orderId).ToString();
            tasks.Add(persistence.Log(Name, key, str));
        }
        // inform related stock actors to reduce the amount because the payment has succeeded
        foreach (var item in invoiceIssued.items)
        {
            var stockActor = GetStockActor(item.seller_id, item.product_id.ToString());
            tasks.Add(stockActor.ConfirmReservation(item.quantity));
        }

        var paymentTs = DateTime.UtcNow;
        var paymentConfirmedWithItems = new PaymentConfirmed(invoiceIssued.customer, invoiceIssued.orderId, invoiceIssued.totalInvoice, invoiceIssued.items, paymentTs, invoiceIssued.instanceId);
        var sellers = invoiceIssued.items.Select(x => x.seller_id).ToHashSet();
        foreach (var sellerID in sellers)
        {
            var sellerActor = GrainFactory.GetGrain<ISellerActor>(sellerID);
            tasks.Add(sellerActor.ProcessPaymentConfirmed(paymentConfirmedWithItems));
        }

        var paymentConfirmedNoItems = new PaymentConfirmed(invoiceIssued.customer, invoiceIssued.orderId, invoiceIssued.totalInvoice, null, paymentTs, invoiceIssued.instanceId);

        tasks.Add(GrainFactory.GetGrain<ICustomerActor>(invoiceIssued.customer.CustomerId).NotifyPaymentConfirmed(paymentConfirmedNoItems));
        tasks.Add(GetOrderActor(invoiceIssued.customer.CustomerId).ProcessPaymentConfirmed(paymentConfirmedNoItems));
        await Task.WhenAll(tasks);

        // proceed to shipment actor
        var shipmentActorID = Helper.GetShipmentActorID(this.customerId, this.config.NumShipmentActors);
        var shipmentActor = GetShipmentActor(shipmentActorID);
        await shipmentActor.ProcessShipment(paymentConfirmedWithItems);
    }

    protected abstract IShipmentActor GetShipmentActor(int id);
    protected abstract IOrderActor GetOrderActor(int id);

    protected abstract IStockActor GetStockActor(int sellerId, string productId);
}
