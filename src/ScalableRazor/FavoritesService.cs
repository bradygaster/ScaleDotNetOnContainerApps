namespace ScalableRazor
{
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
