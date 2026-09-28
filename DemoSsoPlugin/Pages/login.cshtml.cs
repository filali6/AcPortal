using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DemoSsoPlugin.Pages;

public class LoginModel : PageModel
{
    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToPage("/Welcome");

        var props = new AuthenticationProperties { RedirectUri = "/Welcome" };
        return Challenge(props, OpenIdConnectDefaults.AuthenticationScheme);
    }
}