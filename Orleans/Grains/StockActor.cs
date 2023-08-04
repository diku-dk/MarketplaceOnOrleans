using Common.Entities;
using Microsoft.Extensions.Logging;
using Orleans.Interfaces;
using Orleans.Runtime;
using StackExchange.Redis;

namespace Orleans.Grains
{
    public class StockActor : Grain, IStockActor
    {
        // ConfigurationOptions option;

        private readonly IPersistentState<StockItem> item;

        private readonly ILogger<StockActor> _logger;

        public StockActor([PersistentState(
            stateName: "cart",
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

        public async Task<ItemStatus> AttemptReservation(int quantity)
        {
            if (item is null || item.State is null) return ItemStatus.DELETED;
            if (item.State.qty_reserved + quantity > item.State.qty_available) return ItemStatus.OUT_OF_STOCK;
            item.State.qty_reserved += quantity;
            await item.WriteStateAsync();
            return ItemStatus.IN_STOCK;
        }

        public Task CancelReservation(int quantity)
        {
            throw new NotImplementedException();
        }

        public Task ConfirmReservation(int quantity)
        {
            throw new NotImplementedException();
        }

        public async Task DeleteItem()
        {
            this.item.State.data = "false";
            await this.item.WriteStateAsync();
            // TODO publish transaction mark
        }
    }
}
