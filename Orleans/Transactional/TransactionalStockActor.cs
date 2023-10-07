using Common;
using Common.Entities;
using Common.Events;
using Microsoft.Extensions.Logging;
using Orleans.Grains;
using Orleans.Infra;
using Orleans.Transactions.Abstractions;

namespace Orleans.Transactional;

public class TransactionalStockActor : Grain, ITransactionalStockActor
{

    private readonly ITransactionalState<StockItem> item;
    private readonly AppConfig config;
    private readonly ILogger<StockActor> _logger;

    public TransactionalStockActor([TransactionalState(
        stateName: "stock",
        storageName: Constants.OrleansStorage)] ITransactionalState<StockItem> state,
        AppConfig options,
        ILogger<StockActor> _logger)
    {
        this.item = state;
        this.config = options;
        this._logger = _logger;
    }

    public Task SetItem(StockItem item)
    {
        return this.item.PerformUpdate(i => i = item);
    }

    public Task<StockItem> GetItem()
    {
        return this.item.PerformRead(p => p);
    }

    public async Task<ItemStatus> AttemptReservation(CartItem cartItem)
    {
        var item_ = await this.item.PerformRead(p => p);
        if (item_.version.CompareTo(cartItem.Version) != 0)
        {
            return ItemStatus.UNAVAILABLE;
        }
        await this.item.PerformUpdate(i => {
            if (i.qty_reserved + cartItem.Quantity > item_.qty_available)
            {
                throw new InvalidOperationException(
                    $"Reserving {cartItem.Quantity} units from item " +
                    $"\"{i.seller_id}:{i.product_id}\" is not possible." +
                    $" This item has {item_.qty_available} units available.");
              
            }
            i.qty_reserved += cartItem.Quantity;
            i.updated_at = DateTime.UtcNow;
        });
        return ItemStatus.IN_STOCK;
    }

    public Task CancelReservation(int quantity)
    {
        return this.item.PerformUpdate(i => {
            i.qty_reserved -= quantity;
            i.updated_at = DateTime.UtcNow;
        });
    }

    public Task ConfirmReservation(int quantity)
    {
        return this.item.PerformUpdate(i => {
            i.qty_reserved -= quantity;
            i.qty_available -= quantity;
            i.updated_at = DateTime.UtcNow;
        });
    }

    public Task ProcessProductUpdate(ProductUpdated productUpdated)
    {
        return this.item.PerformUpdate(i => {
            i.version = productUpdated.instanceId;
            i.updated_at = DateTime.UtcNow;
        });
    }

    public Task Reset()
    {
        return this.item.PerformUpdate(i => {
            i.qty_reserved = 0;
            i.qty_available = 10000;
            i.version = "0";
            i.updated_at = DateTime.UtcNow;
        });
    }

}

