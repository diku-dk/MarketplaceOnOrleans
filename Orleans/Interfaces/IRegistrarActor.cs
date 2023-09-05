using Common.Entities;

namespace Orleans.Interfaces;

// https://github.com/dotnet/orleans/issues/5281#issuecomment-450410183
public interface IRegistrarActor : IGrainWithIntegerKey
{

    Task AddSeller(Seller seller);

    Task<int> GetNumSellers();

    Task Reset();

}


