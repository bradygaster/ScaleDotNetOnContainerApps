using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ScalableRazor.Pages
{
    public class FavoritesModel : PageModel
    {
        public IEnumerable<GitHubRepo>? Repos { get; set; } = new List<GitHubRepo>();
        private readonly FavoritesService _favoritesService;

        [BindProperty]
        public string UrlToUnfavorite { get; set; }

        public FavoritesModel(FavoritesService favoritesService)
        {
            _favoritesService = favoritesService;
        }

        public async Task OnGetAsync()
        {
            Repos = await _favoritesService.GetFavorites();
        }

        public async Task<IActionResult> OnPostAsync(string Command)
        {
            if (Command == "Unfavorite")
            {
                var favorite = (await _favoritesService.GetFavorites()).FirstOrDefault(f => f.HtmlUrl == UrlToUnfavorite);
                if(favorite != null)
                {
                    await _favoritesService.Unfavorite(favorite);
                }
            }

            Repos = await _favoritesService.GetFavorites();
            return Page();
        }
    }
}
