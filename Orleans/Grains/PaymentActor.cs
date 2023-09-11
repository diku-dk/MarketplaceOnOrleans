using Common.Entities;
using Common.Events;
using Microsoft.Extensions.Logging;
using Orleans.Infra;
using Orleans.Interfaces;
using RocksDbSharp;
using System.Text;
using System.Text.Json;

namespace Orleans.Grains;

internal class PaymentActor : Grain, IPaymentActor
{
    private int customerId;
    readonly ILogger<PaymentActor> _logger;

    readonly RocksDb db;

    public PaymentActor(ILogger<PaymentActor> _logger)
    {
        this._logger = _logger;
        db = RocksDb.Open(Constants.rocksDBOptions, typeof(PaymentActor).FullName);
    }

    public override async Task OnActivateAsync(CancellationToken token)
    {
        this.customerId = (int)this.GetPrimaryKeyLong();
        await base.OnActivateAsync(token);
    }

    public async Task ProcessPayment(InvoiceIssued invoiceIssued)
    {
        int seq = 1;

        var cc = invoiceIssued.customer.PaymentType.Equals(PaymentType.CREDIT_CARD.ToString());

        // create payment tuples
        var orderPayment = new List<OrderPayment>();
        OrderPaymentCard card = null;
        if (cc || invoiceIssued.customer.PaymentType.Equals(PaymentType.DEBIT_CARD.ToString()))
        {
            var cardPaymentLine = new OrderPayment()
            {
                order_id = invoiceIssued.orderId,
                payment_sequential = seq,
                payment_type = cc ? PaymentType.CREDIT_CARD : PaymentType.DEBIT_CARD,
                payment_installments = invoiceIssued.customer.Installments,
                payment_value = invoiceIssued.totalInvoice
            };
            orderPayment.Add(cardPaymentLine);

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

        if (invoiceIssued.customer.PaymentType.Equals(PaymentType.BOLETO.ToString()))
        {
            orderPayment.Add(new OrderPayment()
            {
                order_id = invoiceIssued.orderId,
                payment_sequential = seq,
                payment_type = PaymentType.BOLETO,
                payment_installments = 1,
                payment_value = invoiceIssued.totalInvoice
            });

            seq++;
        }

        // then one line for each voucher
        foreach (var item in invoiceIssued.items)
        {
            foreach (var voucher in item.vouchers)
            {
                orderPayment.Add(new OrderPayment()
                {
                    order_id = invoiceIssued.orderId,
                    payment_sequential = seq,
                    payment_type = PaymentType.VOUCHER,
                    payment_installments = 1,
                    payment_value = voucher
                });

                seq++;
            }
        }

        // Using strings below, but can also use byte arrays for both keys and values
        var str = JsonSerializer.Serialize((orderPayment, card));
        var sb = new StringBuilder(invoiceIssued.customer.CustomerId).Append("-").Append(invoiceIssued.orderId);
        db.Put(sb.ToString(), str);
        _logger.LogWarning($"Log payment info to RocksDB. ");

        // inform related stock actors to reduce the amount because the payment has succeeded
        var tasks = new List<Task>();
        foreach (var item in invoiceIssued.items)
        {
            var stockActor = GrainFactory.GetGrain<IStockActor>(item.seller_id, item.product_id.ToString());
            tasks.Add(stockActor.ConfirmReservation(item.quantity));
        }
        await Task.WhenAll(tasks);
        _logger.LogWarning($"Confirm reservation on {tasks.Count} stock actors. ");

        tasks.Clear();

        var paymentConfirmed = new PaymentConfirmed(invoiceIssued.customer, invoiceIssued.orderId, invoiceIssued.totalInvoice, invoiceIssued.items, DateTime.UtcNow, invoiceIssued.instanceId);
        var sellers = invoiceIssued.items.Select(x => x.seller_id).ToHashSet();
        foreach (var sellerID in sellers)
        {
            var sellerActor = GrainFactory.GetGrain<ISellerActor>(sellerID);
            tasks.Add(sellerActor.ProcessPaymentConfirmed(paymentConfirmed));
        }
        await Task.WhenAll(tasks);
        _logger.LogWarning($"Notify {sellers.Count} sellers PaymentConfirmed. ");

        // proceed to shipment actor
        var shipmentActorID = Helper.GetShipmentActorID(invoiceIssued.customer.CustomerId);
        var shipmentActor = GrainFactory.GetGrain<IShipmentActor>(shipmentActorID);
        await shipmentActor.ProcessShipment(paymentConfirmed);
        _logger.LogWarning($"Notify shipment actor PaymentConfirmed. ");

    }
}