using Common.Config;
using Common.Entities;
using Common.Events;
using Microsoft.Extensions.Logging;
using OrleansApp.Grains;
using OrleansApp.Infra;
using Orleans.Transactions.Abstractions;

namespace OrleansApp.Transactional;

public sealed class TransactionalStockActor : Grain, ITransactionalStockActor
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
        this.item = state ?? throw new ArgumentNullException(nameof(state)); ;
        this.config = options;
        this._logger = _logger;
    }

    public Task SetItem(StockItem item)
    {
        return this.item.PerformUpdate(i => {
            i.seller_id = item.seller_id;
            i.product_id = item.product_id;
            i.version = item.version;
            i.data = item.data;
            i.created_at = DateTime.UtcNow;
            i.qty_available = item.qty_available;
            i.qty_reserved = item.qty_reserved;
            i.ytd = item.ytd;
            });
    }

    public Task<StockItem> GetItem()
    {
        return this.item.PerformRead(p => p);
    }

    public async Task<ItemStatus> AttemptReservation(CartItem cartItem)
    {
        var status = await this.item.PerformUpdate(i => {

            if (i.version.CompareTo(cartItem.Version) != 0)
            {
                return ItemStatus.UNAVAILABLE;
            }

            if (i.qty_reserved + cartItem.Quantity > i.qty_available)
            {
                return ItemStatus.OUT_OF_STOCK;
            }
            i.qty_reserved += cartItem.Quantity;
            i.updated_at = DateTime.UtcNow;
            return ItemStatus.IN_STOCK;
        });
        return status;
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
            i.version = 0.ToString();
            i.updated_at = DateTime.UtcNow;
        });
    }

}

