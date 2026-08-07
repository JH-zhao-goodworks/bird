using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BirdHotel.Web.Pages;

[AllowAnonymous]
public class LoginModel(IConfiguration configuration) : PageModel
{
    [BindProperty]
    public string Password { get; set; } = "";

    public string? ErrorMessage { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        // 合言葉は環境変数 APP_PASSWORD で設定する（店舗で共有する1つのパスワード）
        var expected = Environment.GetEnvironmentVariable("APP_PASSWORD") ?? configuration["AppPassword"];
        if (string.IsNullOrEmpty(expected))
        {
            ErrorMessage = "パスワードが設定されていません。環境変数 APP_PASSWORD を設定してください。";
            return Page();
        }

        if (Password != expected)
        {
            ErrorMessage = "合言葉が違います。";
            return Page();
        }

        var claims = new List<Claim> { new(ClaimTypes.Name, "スタッフ") };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });

        return LocalRedirect(returnUrl ?? "/");
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Login");
    }
}
