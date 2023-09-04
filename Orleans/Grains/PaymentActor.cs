using Common.Entities;
using Common.Events;
using Common.Integration;
using Microsoft.Extensions.Logging;
using Orleans.Infra;
using Orleans.Interfaces;

namespace Orleans.Grains;

internal class PaymentActor : Grain, IPaymentActor
{
    int customerId;
    Dictionary<int, List<OrderPayment>> payments;    // <orderId, one record per paid card>
    Dictionary<int, OrderPaymentCard> paymentCards;  // <orderId, cards used for the payment> 
    ILogger<PaymentActor> _logger;

    public PaymentActor(ILogger<PaymentActor> _logger)
    {
        this._logger = _logger;
    }

    public override async Task OnActivateAsync(CancellationToken token)
    {
        customerId = (int)this.GetPrimaryKeyLong();
        payments = new Dictionary<int, List<OrderPayment>>();
        paymentCards = new Dictionary<int, OrderPaymentCard>();
        await base.OnActivateAsync(token);
    }

    public async Task ProcessPayment(InvoiceIssued invoiceIssued)
    {
        /*
         * We assume the payment provider exposes an idempotency ID 
         * that guarantees exactly once payment processing even when 
         * a payment request is submitted more than once to them
         */
        var now = DateTime.UtcNow;

        // https://stackoverflow.com/questions/49727809
        var cardExpParsed = DateTime.UtcNow; //DateTime.ParseExact(invoiceIssued.customer.CardExpiration, "MMyy", CultureInfo.InvariantCulture);

        var options = new PaymentIntentCreateOptions()
        {
            Amount = invoiceIssued.totalInvoice,
            Customer = invoiceIssued.customer.CustomerId.ToString(),
            IdempotencyKey = invoiceIssued.invoiceNumber,
            cardOptions = new()
            {
                Number = invoiceIssued.customer.CardNumber,
                Cvc = invoiceIssued.customer.CardSecurityNumber,
                ExpMonth = cardExpParsed.Month.ToString(),
                ExpYear = cardExpParsed.Year.ToString()
            }
        };

        // Based on: https://stripe.com/docs/payments/payment-intents/verifying-status
        var status = PaymentStatus.succeeded;

        int seq = 1;

        var cc = invoiceIssued.customer.PaymentType.Equals(PaymentType.CREDIT_CARD.ToString());

        // create payment tuples
        if (!payments.ContainsKey(invoiceIssued.orderId)) payments.Add(invoiceIssued.orderId, new List<OrderPayment>());
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
            payments[invoiceIssued.orderId].Add(cardPaymentLine);

            // create an entity for credit card payment details with FK to order payment
            var card = new OrderPaymentCard()
            {
                order_id = invoiceIssued.orderId,
                payment_sequential = seq,
                card_number = invoiceIssued.customer.CardNumber,
                card_holder_name = invoiceIssued.customer.CardHolderName,
                card_expiration = invoiceIssued.customer.CardExpiration,
                card_brand = invoiceIssued.customer.CardBrand
            };

            paymentCards.Add(invoiceIssued.orderId, card);

            seq++;
        }

        if (invoiceIssued.customer.PaymentType.Equals(PaymentType.BOLETO.ToString()))
        {
            payments[invoiceIssued.orderId].Add(new OrderPayment()
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
                payments[invoiceIssued.orderId].Add(new OrderPayment()
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

        // inform related stock actors to reduce the amount because the payment has succeeded
        var tasks = new List<Task>();
        foreach (var item in invoiceIssued.items)
        {
            var stockActor = GrainFactory.GetGrain<IStockActor>(item.seller_id, item.product_id.ToString());
            tasks.Add(stockActor.ConfirmReservation(item.quantity));
        }
        await Task.WhenAll(tasks);
        _logger.LogWarning($"Confirm reservation on {tasks.Count} stock actors. ");

        // proceed to shipment actor
        var paymentConfirmed = new PaymentConfirmed(invoiceIssued.customer, invoiceIssued.orderId, invoiceIssued.totalInvoice, invoiceIssued.items, now, invoiceIssued.instanceId);
        var shipmentActorID = Helper.GetShipmentActorID(invoiceIssued.customer.CustomerId);
        var shipmentActor = GrainFactory.GetGrain<IShipmentActor>(shipmentActorID);
        await shipmentActor.ProcessShipment(paymentConfirmed);
        _logger.LogWarning($"Notify shipment actor PaymentConfirmed. ");

        tasks.Clear();
        var sellers = invoiceIssued.items.Select(x => x.seller_id).ToHashSet();
        foreach (var sellerID in sellers)
        {
            var sellerActor = GrainFactory.GetGrain<ISellerActor>(sellerID);
            tasks.Add(sellerActor.ProcessPaymentConfirmed(paymentConfirmed));
        }
        await Task.WhenAll(tasks);
        _logger.LogWarning($"Notify {sellers.Count} sellers PaymentConfirmed. ");
    }
}