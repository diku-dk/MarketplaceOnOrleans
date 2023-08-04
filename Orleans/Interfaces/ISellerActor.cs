using Common.Entities;
using Common.Events;

namespace Orleans.Interfaces
{
    public interface ISellerActor : IGrainWithIntegerKey
    {
        public Task IndexProduct(int product_id);
    }
}
