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

    public class FavoritesService
    {
        private IHttpContextAccessor _httpContextAccessor;
        private IGrainFactory _grainFactory;
        private string _favoritesCookieName = "favorites";

        public FavoritesService(IHttpContextAccessor httpContextAccessor, IGrainFactory grainFactory)
        {
            _httpContextAccessor = httpContextAccessor;
            _grainFactory = grainFactory;
        }

        public bool HasCookie() => 
            _httpContextAccessor.HttpContext.Request.Cookies.ContainsKey(_favoritesCookieName);

        public void SetCookie()
            => _httpContextAccessor.HttpContext.Response.Cookies.Append(_favoritesCookieName, Guid.NewGuid().ToString());

        public Guid GetCookie()
        {
            if (HasCookie())
                return Guid.Parse(_httpContextAccessor.HttpContext.Request.Cookies[_favoritesCookieName]);
            else
            {
                SetCookie();
                return Guid.Parse(_httpContextAccessor.HttpContext.Request.Cookies[_favoritesCookieName]);
            }
        }

        public async Task<bool> IsFavorite(GitHubRepo repo)
        {
            var grain = _grainFactory.GetGrain<IFavoritesGrain>(GetCookie());
            var favorites = await grain.GetFavorites();
            return favorites.Any(x => x.HtmlUrl == repo.HtmlUrl);
        }

        public async Task Favorite(GitHubRepo repo)
        {
            var grain = _grainFactory.GetGrain<IFavoritesGrain>(GetCookie());
            await grain.Favorite(repo);
        }

        public async Task Unfavorite(GitHubRepo repo)
        {
            var grain = _grainFactory.GetGrain<IFavoritesGrain>(GetCookie());
            await grain.UnFavorite(repo);
        }

        public async Task<List<GitHubRepo>> GetFavorites()
        {
            var grain = _grainFactory.GetGrain<IFavoritesGrain>(GetCookie());
            return await grain.GetFavorites();
        }
    }
}
