using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Net.Http.Headers;
using System.Text.Json;


namespace ScalableRazor.Pages
{
    public class IndexModel : PageModel
    {
        private const string SESSION_KEY_FOR_REPOS = "Repos";
        private const string SESSION_KEY_FOR_LASTSEARCH = "LastSearch";
        private const string SESSION_KEY_FOR_RECENTSEARCHES = "RecentSearches";
        private readonly IConfiguration _env;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IDistributedCache _distributedCache;
        private readonly ILogger<IndexModel> _logger;
        private readonly FavoritesService _favoritesService;

        public IndexModel(IConfiguration env,
            IHttpClientFactory httpFactory,
            IDistributedCache distributedCache,
            ILogger<IndexModel> logger,
            FavoritesService favoritesService)
        {
            _env = env;
            _httpFactory = httpFactory;
            _distributedCache = distributedCache;
            _logger = logger;
            _favoritesService = favoritesService;
        }

        [BindProperty]
        public string? SearchTerm { get; set; }

        [BindProperty]
        public string FavoriteUrl { get; set; }

        public List<GitHubRepo>? Repos { get; set; } = new List<GitHubRepo>();
        public List<GitHubRepo>? Favorites { get; set; } = new List<GitHubRepo>();
        public List<string>? RecentSearches { get; set; } = new List<string>();

        public async Task<IActionResult> OnGet()
        {
            if (!_favoritesService.HasCookie(HttpContext))
            {
                _favoritesService.SetCookie(HttpContext);
            }

            await RefreshUIFromSession();

            return Page();
        }

        public async Task<IActionResult> OnPost(string Command)
        {
            if (Command == "Search")
            {
                await Search();
            }
            else if (Command == "Favorite")
            {
                await AddToFavorites(FavoriteUrl);
            }
            else if (Command == "Unfavorite")
            {
                await RemoveFromFavorites(FavoriteUrl);
            }
            else
            {
                SearchTerm = Command;
                await Search();
            }

            await RefreshUIFromSession();
            
            return Page();
        }

        private async Task RefreshUIFromSession()
        {
            if (HttpContext.Session.Keys.Contains(SESSION_KEY_FOR_REPOS))
            {
                Repos = JsonSerializer.Deserialize<List<GitHubRepo>>(HttpContext.Session.GetString(SESSION_KEY_FOR_REPOS));
            }
            if (HttpContext.Session.Keys.Contains(SESSION_KEY_FOR_LASTSEARCH))
            {
                SearchTerm = HttpContext.Session.GetString(SESSION_KEY_FOR_LASTSEARCH);
            }
            if (HttpContext.Session.Keys.Contains(SESSION_KEY_FOR_RECENTSEARCHES))
            {
                RecentSearches = JsonSerializer.Deserialize<List<string>>(HttpContext.Session.GetString(SESSION_KEY_FOR_RECENTSEARCHES));
            }

            try
            {
                Favorites = await _favoritesService.GetFavorites(HttpContext);
            }
            catch (ArgumentNullException)
            {
                Favorites = new List<GitHubRepo>();
            }
        }

        private async Task Search()
        {
            var reposJson = await _distributedCache.GetStringAsync(SearchTerm);

            if (!string.IsNullOrEmpty(reposJson))
            {
                Repos = JsonSerializer.Deserialize<List<GitHubRepo>>(reposJson);
                _logger.LogInformation($"Cache hit for {SearchTerm}");
            }
            else
            {
                _logger.LogInformation($"Cache miss for {SearchTerm}");

                var client = _httpFactory.CreateClient();

                var gitHubUrl = $"{_env["GitHubUrl"]}/orgs/{SearchTerm}/repos";

                // GitHub API wants a UserAgent specified
                var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, gitHubUrl)
                {
                    Headers =
                    {
                        { HeaderNames.UserAgent, "dotnet" }
                    }
                };

                var httpResponseMessage = await client.SendAsync(httpRequestMessage);

                if (httpResponseMessage.IsSuccessStatusCode)
                {
                    using var contentStream =
                        await httpResponseMessage.Content.ReadAsStreamAsync();

                    if (contentStream != null)
                    {
                        Repos = await JsonSerializer.DeserializeAsync<List<GitHubRepo>>(contentStream);

                        // stash the results into session so they can be remembered as the user favorites things
                        var json = JsonSerializer.Serialize(Repos);
                        HttpContext.Session.SetString(SESSION_KEY_FOR_REPOS, json);
                        HttpContext.Session.SetString(SESSION_KEY_FOR_LASTSEARCH, SearchTerm);
                        await _distributedCache.SetStringAsync(SearchTerm, json);
                    }
                }
            }
            
            if (HttpContext.Session.Keys.Contains(SESSION_KEY_FOR_RECENTSEARCHES))
                RecentSearches = JsonSerializer.Deserialize<List<string>>(HttpContext.Session.GetString(SESSION_KEY_FOR_RECENTSEARCHES));

            if (!RecentSearches.Contains(SearchTerm))
            {
                RecentSearches.Add(SearchTerm);
                HttpContext.Session.SetString(SESSION_KEY_FOR_RECENTSEARCHES, JsonSerializer.Serialize(RecentSearches));
            }
        }

        public async Task AddToFavorites(string url)
        {
            await RefreshUIFromSession();

            var repo = Repos.FirstOrDefault(r => r.HtmlUrl == url);
            if (await _favoritesService.IsFavorite(repo, HttpContext))
            {
                await _favoritesService.Unfavorite(repo, HttpContext);
            }
            else
            {
                await _favoritesService.Favorite(repo, HttpContext);
            }
        }

        private async Task RemoveFromFavorites(string url)
        {
            await RefreshUIFromSession();
            
            var repo = Repos.FirstOrDefault(r => r.HtmlUrl == url);
            await _favoritesService.Unfavorite(repo, HttpContext);
        }
    }
}