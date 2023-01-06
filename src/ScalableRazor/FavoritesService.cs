using Orleans.Runtime;

namespace ScalableRazor
{
    public static class FavoritesServiceServiceCollectionExtensions
    {
        public static WebApplicationBuilder AddFavoritesService(this WebApplicationBuilder builder)
        {
            builder.Services.AddScoped<FavoritesService>();

            var storageConnectionString = string.IsNullOrEmpty(builder.Configuration.GetValue<string>("AZURE_STORAGE_CONNECTION_STRING"))
                                        ? "UseDevelopmentStorage=true"
                                        : builder.Configuration.GetValue<string>("AZURE_STORAGE_CONNECTION_STRING");

            // wire up the orleans silo
            builder.Services.AddOrleans(siloBuilder =>
            {
                siloBuilder
                    .UseAzureStorageClustering(azureStorageConfigurationOptions =>
                    {
                        azureStorageConfigurationOptions.ConfigureTableServiceClient(storageConnectionString);
                    })
                    .AddAzureBlobGrainStorageAsDefault(azureBlobGrainStorageOptions =>
                    {
                        azureBlobGrainStorageOptions.ContainerName = "grainstorage";
                        azureBlobGrainStorageOptions.ConfigureBlobServiceClient(storageConnectionString);
                    });
            });

            return builder;
        }
    }

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

            if (_grainState.State.Any(x => x.HtmlUrl == repo.HtmlUrl))
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
