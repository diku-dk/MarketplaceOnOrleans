using Orleans.Runtime;

namespace Orleans.Grains
{
    public sealed class SimpleGrain : Grain, IPersistentGrain
    {

        private readonly IPersistentState<UrlDetails> state;
        public SimpleGrain(
                [PersistentState(
            stateName: "url",
            storageName: "OrleansStorage")]
            IPersistentState<UrlDetails> state)
        {
            this.state = state; 
        }

        public async Task SetUrl(string fullUrl)
        {
            state.State = new()
            {
                ShortenedRouteSegment = this.GetPrimaryKeyString(),
                FullUrl = fullUrl
            };

            await state.WriteStateAsync();
        }

        public Task<string> GetUrl() =>
            Task.FromResult(state.State.FullUrl);
    }

    public class UrlDetails
    {

        public string FullUrl { get; set; }

        public string ShortenedRouteSegment { get; set; }
    }
}
