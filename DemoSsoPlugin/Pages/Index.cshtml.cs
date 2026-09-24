using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DemoSsoPlugin.Pages;

public class IndexModel : PageModel
{
    public string RedirectTarget => User.Identity?.IsAuthenticated == true ? "/Welcome" : "/Login";

    public void OnGet()
    {
    }
}