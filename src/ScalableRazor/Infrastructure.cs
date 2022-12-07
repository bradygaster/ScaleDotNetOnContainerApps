using Orleans.Runtime;

namespace ScalableRazor
{
    public interface IFavoritesGrain : IGrainWithGuidKey
    {
        Task<List<GitHubRepo>> GetFavorites();
        Task Favorite(GitHubRepo repo);
        Task UnFavorite(GitHubRepo repo);
    }

    public class FavoritesGrain : Grain, IFavoritesGrain
    {
        private ILogger<FavoritesGrain> _logger;
        private IPersistentState<List<GitHubRepo>> _grainState;

        public FavoritesGrain(ILogger<FavoritesGrain> logger,
            [PersistentState("Favorites")]
            IPersistentState<List<GitHubRepo>> grainState)
        {
            _logger = logger;
            _grainState = grainState;
        }

        public async Task Favorite(GitHubRepo repo)
        {
            await _grainState.ReadStateAsync();
            
            if(_grainState.State.Any(x => x.HtmlUrl == repo.HtmlUrl))
            {
                _logger.LogInformation("Repo already favorited");
            }
            else
            {
                _grainState.State.Add(repo);
                _logger.LogInformation("Repo favorited");
                await _grainState.WriteStateAsync();
            }
        }

        public async Task<List<GitHubRepo>> GetFavorites()
        {
            await _grainState.ReadStateAsync();
            var result = _grainState.State;
            return result;
        }

        public async Task UnFavorite(GitHubRepo repo)
        {
            await _grainState.ReadStateAsync();

            if (_grainState.State.Any(x => x.HtmlUrl == repo.HtmlUrl))
            {
                _grainState.State.RemoveAll(x => x.HtmlUrl == repo.HtmlUrl);
                _logger.LogInformation("Repo unfavorited");
                await _grainState.WriteStateAsync();
            }
        }
    }
}
