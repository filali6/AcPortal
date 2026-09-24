using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DemoSsoPlugin.Pages;

[Authorize]
public class WelcomeModel : PageModel
{
    public string DisplayName =>
        User.FindFirst("name")?.Value
        ?? User.FindFirst("preferred_username")?.Value
        ?? "utilisateur";

    public void OnGet()
    {
    }
}