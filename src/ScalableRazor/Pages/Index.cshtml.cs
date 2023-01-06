using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
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
        private readonly IMemoryCache _memoryCache;
        private readonly FavoritesService _favoritesService;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(IConfiguration env,
            IHttpClientFactory httpFactory,
            IMemoryCache memoryCache,
            FavoritesService favoritesService,
            ILogger<IndexModel> logger)
        {
            _env = env;
            _httpFactory = httpFactory;
            _memoryCache = memoryCache;
            _favoritesService = favoritesService;
            _logger = logger;
        }

        [BindProperty]
        public string? SearchTerm { get; set; }

        [BindProperty]
        public string FavoriteUrl { get; set; }

        public IEnumerable<GitHubRepo>? Repos { get; set; } = new List<GitHubRepo>();
        public IEnumerable<GitHubRepo>? Favorites { get; set; } = new List<GitHubRepo>();
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

        private async Task RefreshUIFromSession()
        {
            if (HttpContext.Session.Keys.Contains(SESSION_KEY_FOR_REPOS))
            {
                Repos = JsonSerializer.Deserialize<IEnumerable<GitHubRepo>>(HttpContext.Session.GetString(SESSION_KEY_FOR_REPOS));
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
            catch(ArgumentNullException)
            {
                Favorites = new List<GitHubRepo>();
            }
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

        private async Task Search()
        {
            IEnumerable<GitHubRepo> tmpRepos = null;
            
            if (_memoryCache.TryGetValue<IEnumerable<GitHubRepo>>(SearchTerm, out tmpRepos))
            {
                _logger.LogInformation($"Cache hit for {SearchTerm}");
                Repos = tmpRepos;
                
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
                        Repos = await JsonSerializer.DeserializeAsync<IEnumerable<GitHubRepo>>(contentStream);

                        // stash the results into session so they can be remembered as the user favorites things
                        var json = JsonSerializer.Serialize(Repos);
                        HttpContext.Session.SetString(SESSION_KEY_FOR_REPOS, json);
                        HttpContext.Session.SetString(SESSION_KEY_FOR_LASTSEARCH, SearchTerm);
                        _memoryCache.Set(SearchTerm, Repos);
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