namespace ScalableRazor
{
    public class FavoritesService
    {
        private IGrainFactory _grainFactory;
        private string _favoritesCookieName = "favorites";

        public FavoritesService(IGrainFactory grainFactory)
        {
            _grainFactory = grainFactory;
        }

        public bool HasCookie(HttpContext httpContext) =>
            httpContext.Request.Cookies.ContainsKey(_favoritesCookieName);

        public void SetCookie(HttpContext httpContext)
            => httpContext.Response.Cookies.Append(_favoritesCookieName, Guid.NewGuid().ToString());

        public Guid GetCookie(HttpContext httpContext)
        {
            if (HasCookie(httpContext))
                return Guid.Parse(httpContext.Request.Cookies[_favoritesCookieName]);
            else
            {
                SetCookie(httpContext);
                return Guid.Parse(httpContext.Request.Cookies[_favoritesCookieName]);
            }
        }

        public async Task<bool> IsFavorite(GitHubRepo repo, HttpContext httpContext)
        {
            var grain = _grainFactory.GetGrain<IFavoritesGrain>(GetCookie(httpContext));
            var favorites = await grain.GetFavorites();
            return favorites.Any(x => x.HtmlUrl == repo.HtmlUrl);
        }

        public async Task Favorite(GitHubRepo repo, HttpContext httpContext)
        {
            var grain = _grainFactory.GetGrain<IFavoritesGrain>(GetCookie(httpContext));
            await grain.Favorite(repo);
        }

        public async Task Unfavorite(GitHubRepo repo, HttpContext httpContext)
        {
            var grain = _grainFactory.GetGrain<IFavoritesGrain>(GetCookie(httpContext));
            await grain.UnFavorite(repo);
        }

        public async Task<List<GitHubRepo>> GetFavorites(HttpContext httpContext)
        {
            var grain = _grainFactory.GetGrain<IFavoritesGrain>(GetCookie(httpContext));
            return await grain.GetFavorites();
        }
    }
}
