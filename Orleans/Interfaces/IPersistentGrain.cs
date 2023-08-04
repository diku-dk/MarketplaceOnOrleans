using Orleans;

public interface IPersistentGrain : IGrainWithIntegerKey
{
    Task SetUrl(string fullUrl);

    Task<string> GetUrl();
}