using Common;
using Common.Entities;
using Common.Events;
using Microsoft.Extensions.Logging;
using Orleans.Concurrency;
using Orleans.Infra;
using Orleans.Interfaces;
using Orleans.Runtime;

namespace Orleans.Grains;

[Reentrant]
public sealed class StockActor : Grain, IStockActor
{

    private readonly IPersistentState<StockItem> item;
    private readonly AppConfig config;
    private readonly ILogger<StockActor> _logger;

    public StockActor([PersistentState(
        stateName: "stock",
        storageName: Constants.OrleansStorage)] IPersistentState<StockItem> state,
        AppConfig options,
        ILogger<StockActor> _logger)
    {
        this.item = state;
        this.config = options;
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
        if(config.OrleansStorage)
            await this.item.WriteStateAsync();
    }

    public async Task<ItemStatus> AttemptReservation(CartItem cartItem)
    {
        if (item.State.version.CompareTo(cartItem.Version) != 0) return ItemStatus.UNAVAILABLE;
        if (item.State.qty_reserved + cartItem.Quantity > item.State.qty_available) {
            _logger.LogWarning("Stock {0}:{1} running out", item.State.seller_id, item.State.product_id);
            return ItemStatus.OUT_OF_STOCK;
        }
        item.State.qty_reserved += cartItem.Quantity;
        item.State.updated_at = DateTime.UtcNow;
        if(config.OrleansStorage)
            await item.WriteStateAsync();
        return ItemStatus.IN_STOCK;
    }

    public async Task CancelReservation(int quantity)
    {
        item.State.qty_reserved -= quantity;
        item.State.updated_at = DateTime.UtcNow;
        if(config.OrleansStorage)
            await item.WriteStateAsync();
    }

    public async Task ConfirmReservation(int quantity)
    {
        item.State.qty_reserved -= quantity;
        item.State.qty_available -= quantity;
        item.State.updated_at = DateTime.UtcNow;
        if(config.OrleansStorage)
            await item.WriteStateAsync();
    }

    public async Task ProcessProductUpdate(ProductUpdated productUpdated)
    {
        item.State.version = productUpdated.instanceId;
        item.State.updated_at = DateTime.UtcNow;
        if(config.OrleansStorage)
            await item.WriteStateAsync();
    }

    public Task<StockItem> GetItem()
    {
        return Task.FromResult(item.State);
    }

    public async Task Reset()
    {
        item.State.qty_reserved = 0;
        item.State.qty_available = 10000;
        item.State.updated_at = DateTime.UtcNow;
        item.State.version = "0";
        if(config.OrleansStorage)
            await item.WriteStateAsync();
    }
}
