using Common.Events;

namespace Orleans.Interfaces
{
    public interface IOrderActor : IGrainWithIntegerKey
    {
        public Task Checkout(ReserveStock reserveStock);
    }
}
