using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Net.Http.Headers;
using System.Text.Json;


namespace ScalableRazor.Pages
{
    public class IndexModel : PageModel
    {
        private const string SESSION_KEY_FOR_REPOS = "Repos";
        private const string SESSION_KEY_FOR_LASTSEARCH = "LastSearch";
        private readonly IConfiguration _env;
        private readonly IHttpClientFactory _httpFactory;

        public IndexModel(IConfiguration env, IHttpClientFactory httpFactory)
        {
            _env = env;
            _httpFactory = httpFactory;
        }

        [BindProperty]
        public string? SearchTerm { get; set; }

        [BindProperty]
        public string FavoriteUrl { get; set; }

        public IEnumerable<GitHubRepo>? Repos { get; set; } = new List<GitHubRepo>();

        public IActionResult OnGet()
        {
            RefreshUIFromSession();

            return Page();
        }

        private void RefreshUIFromSession()
        {
            if (HttpContext.Session.Keys.Contains(SESSION_KEY_FOR_REPOS))
            {
                Repos = JsonSerializer.Deserialize<IEnumerable<GitHubRepo>>(HttpContext.Session.GetString(SESSION_KEY_FOR_REPOS));
            }
            if (HttpContext.Session.Keys.Contains(SESSION_KEY_FOR_LASTSEARCH))
            {
                SearchTerm = HttpContext.Session.GetString(SESSION_KEY_FOR_LASTSEARCH);
            }
        }

        public async Task<IActionResult> OnPost(string Command)
        {
            if(Command == "Search")
            {
                await Search();
            }
            
            if(Command == "Favorite")
            {
                await AddToFavorites(FavoriteUrl);
            }

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

                if(contentStream != null)
                {
                    Repos = await JsonSerializer.DeserializeAsync<IEnumerable<GitHubRepo>>(contentStream);

                    // stash the results into session so they can be remembered as the user favorites things
                    var json = JsonSerializer.Serialize(Repos);
                    HttpContext.Session.SetString(SESSION_KEY_FOR_REPOS, json);
                    HttpContext.Session.SetString(SESSION_KEY_FOR_LASTSEARCH, SearchTerm);
                }
            }
        }

        public Task AddToFavorites(string url)
        {
            var listOfFavorites = new List<string>();
            RefreshUIFromSession();
            return Task.CompletedTask;
        }
    }
}