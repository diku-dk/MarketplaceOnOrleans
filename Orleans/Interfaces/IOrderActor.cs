using Common.Events;
using Orleans.Concurrency;

namespace Orleans.Interfaces
{
    public interface IOrderActor : IGrainWithIntegerCompoundKey
    {
        [OneWay]
        Task Checkout(ReserveStock reserveStock);
    }
}