using Common.Entities;
using Common.Events;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;
using OrleansApp.Infra;
using OrleansApp.Interfaces;
using Orleans.Runtime;
using Common.Config;

namespace OrleansApp.Grains;

[Reentrant]
public sealed class CustomerActor : Grain, ICustomerActor
{
    private readonly AppConfig config;
    private readonly IPersistentState<Customer> customer;
    private int customerId;

    private readonly ILogger<CustomerActor> _logger;

    public CustomerActor([PersistentState(
        stateName: "customer",
        storageName: Constants.OrleansStorage)]
        IPersistentState<Customer> state,
        AppConfig options,
        ILogger<CustomerActor> _logger)
    {
        this.customer = state;
        this.config = options;
        this._logger = _logger;
    }

    public override Task OnActivateAsync(CancellationToken token)
    {
        this.customerId = (int) this.GetPrimaryKeyLong();
        return Task.CompletedTask;
    }

    public async Task SetCustomer(Customer customer)
    {
        this.customer.State = customer;
        await this.customer.WriteStateAsync();
    }

    public async Task Clear()
    {
        await this.customer.ClearStateAsync();
    }

    public Task<Customer> GetCustomer()
    {
        return Task.FromResult(this.customer.State);
    }

    public async Task NotifyDelivery(DeliveryNotification deliveryNotificationd)
    {
        this.customer.State.delivery_count++;
        if(config.OrleansStorage)
            await this.customer.WriteStateAsync();
    }

    public async Task NotifyPaymentFailed(PaymentFailed paymentFailed)
    {
        this.customer.State.failed_payment_count++;
        if(config.OrleansStorage)
            await this.customer.WriteStateAsync();
    }

    public async Task NotifyPaymentConfirmed(PaymentConfirmed paymentConfirmed)
    {
        this.customer.State.success_payment_count++;
        if(config.OrleansStorage)
            await this.customer.WriteStateAsync();
    }

}
