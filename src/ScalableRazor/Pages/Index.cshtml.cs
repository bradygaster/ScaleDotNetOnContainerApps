using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Net.Http.Headers;
using System;
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
        private readonly FavoritesService _favoritesService;

        public IndexModel(IConfiguration env, IHttpClientFactory httpFactory, FavoritesService favoritesService)
        {
            _env = env;
            _httpFactory = httpFactory;
            _favoritesService = favoritesService;
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
            if (!_favoritesService.HasCookie())
            {
                _favoritesService.SetCookie();
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

            Favorites = await _favoritesService.GetFavorites();
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

                    if (HttpContext.Session.Keys.Contains(SESSION_KEY_FOR_RECENTSEARCHES))
                        RecentSearches = JsonSerializer.Deserialize<List<string>>(HttpContext.Session.GetString(SESSION_KEY_FOR_RECENTSEARCHES));

                    if (!RecentSearches.Contains(SearchTerm))
                    {
                        RecentSearches.Add(SearchTerm);
                        HttpContext.Session.SetString(SESSION_KEY_FOR_RECENTSEARCHES, JsonSerializer.Serialize(RecentSearches));
                    }
                }
            }
        }

        public async Task AddToFavorites(string url)
        {
            await RefreshUIFromSession();

            var repo = Repos.FirstOrDefault(r => r.HtmlUrl == url);
            if (await _favoritesService.IsFavorite(repo))
            {
                await _favoritesService.Unfavorite(repo);
            }
            else
            {
                await _favoritesService.Favorite(repo);
            }
        }

        private async Task RemoveFromFavorites(string url)
        {
            await RefreshUIFromSession();
            
            var repo = Repos.FirstOrDefault(r => r.HtmlUrl == url);
            await _favoritesService.Unfavorite(repo);
        }
    }
}