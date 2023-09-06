using Common.Entities;
using Common.Events;
using Microsoft.Extensions.Logging;
using Orleans.Interfaces;
using Orleans.Runtime;

namespace Orleans.Grains;

public class StockActor : Grain, IStockActor
{
    // ConfigurationOptions option;

    private readonly IPersistentState<StockItem> item;

    private readonly ILogger<StockActor> _logger;

    public StockActor([PersistentState(
        stateName: "stock",
        storageName: Infra.Constants.OrleansStorage)] IPersistentState<StockItem> state,
       ILogger<StockActor> _logger)
    {
        this.item = state;
        this._logger = _logger;
    }

    public override async Task OnActivateAsync(CancellationToken token)
    {
        // get redis connection string from metadata grain. publish TransactionMark after delete. can dispose itself after
        await base.OnActivateAsync(token);
    }

    public async Task SetItem(StockItem item)
    {
        this.item.State = item;
        await this.item.WriteStateAsync();
    }

    public async Task<ItemStatus> AttemptReservation(CartItem cartItem)
    {
        if (item.State.version != cartItem.Version) return ItemStatus.UNAVAILABLE;
        if (item.State.qty_reserved + cartItem.Quantity > item.State.qty_available) return ItemStatus.OUT_OF_STOCK;
        item.State.qty_reserved += cartItem.Quantity;
        await item.WriteStateAsync();
        return ItemStatus.IN_STOCK;
    }

    public async Task CancelReservation(int quantity)
    {
        item.State.qty_reserved += quantity;
        await item.WriteStateAsync();
    }

    public async Task ConfirmReservation(int quantity)
    {
        item.State.qty_reserved -= quantity;
        item.State.qty_available -= quantity;
        await item.WriteStateAsync();
    }

    public async Task ProcessProductUpdate(ProductUpdated productUpdated)
    {
        item.State.version = productUpdated.instanceId;
        await item.WriteStateAsync();
    }
}
