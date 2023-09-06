using Common.Entities;
using Common.Events;
using Microsoft.Extensions.Logging;
using Orleans.Infra;
using Orleans.Interfaces;
using Orleans.Runtime;

namespace Orleans.Grains;

public class StockActor : Grain, IStockActor
{

    private readonly IPersistentState<StockItem> item;

    private readonly ILogger<StockActor> _logger;

    public StockActor([PersistentState(
        stateName: "stock",
        storageName: Constants.OrleansStorage)] IPersistentState<StockItem> state,
       ILogger<StockActor> _logger)
    {
        this.item = state;
        this._logger = _logger;
    }

    public override Task OnActivateAsync(CancellationToken token)
    {
        return Task.CompletedTask;
    }

    public async Task SetItem(StockItem item)
    {
        this.item.State = item;
        this.item.State.created_at = DateTime.UtcNow;
        await this.item.WriteStateAsync();
    }

    public async Task<ItemStatus> AttemptReservation(CartItem cartItem)
    {
        if (item.State.version != cartItem.Version) return ItemStatus.UNAVAILABLE;
        if (item.State.qty_reserved + cartItem.Quantity > item.State.qty_available) return ItemStatus.OUT_OF_STOCK;
        item.State.qty_reserved += cartItem.Quantity;
        item.State.updated_at = DateTime.UtcNow;
        await item.WriteStateAsync();
        return ItemStatus.IN_STOCK;
    }

    public async Task CancelReservation(int quantity)
    {
        item.State.qty_reserved += quantity;
        item.State.updated_at = DateTime.UtcNow;
        await item.WriteStateAsync();
    }

    public async Task ConfirmReservation(int quantity)
    {
        item.State.qty_reserved -= quantity;
        item.State.qty_available -= quantity;
        item.State.updated_at = DateTime.UtcNow;
        await item.WriteStateAsync();
    }

    public async Task ProcessProductUpdate(ProductUpdated productUpdated)
    {
        item.State.version = productUpdated.instanceId;
        item.State.updated_at = DateTime.UtcNow;
        await item.WriteStateAsync();
    }

    public Task<StockItem> GetItem()
    {
        return Task.FromResult(item.State);
    }
}
