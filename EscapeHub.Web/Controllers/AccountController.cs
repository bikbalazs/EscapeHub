using System.Security.Claims;
using EscapeHub.Core.Entities;
using EscapeHub.Infrastructure.Data;
using EscapeHub.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EscapeHub.Web.Controllers;

public sealed class AccountController(
    EscapeHubDbContext db,
    IPasswordHasher<User> passwordHasher) : Controller
{
    [HttpGet]
    public IActionResult Register() => View(new RegisterModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var email = model.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email))
        {
            ModelState.AddModelError(nameof(model.Email), "Ezzel az e-mail-címmel már regisztráltak.");
            return View(model);
        }
        var user = new User { Id = Guid.NewGuid(), Email = email };
        user.PasswordHash = passwordHasher.HashPassword(user, model.Password);
        db.Users.Add(user);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(model.Email), "Ezzel az e-mail-címmel már regisztráltak.");
            return View(model);
        }
        await SignIn(user);
        return RedirectToAction("Index", "Rooms");
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);
        var email = model.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email);
        if (user is null || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Hibás e-mail-cím vagy jelszó.");
            return View(model);
        }
        await SignIn(user);
        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction("Index", "Rooms");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Rooms");
    }

    [HttpGet]
    public IActionResult Denied() => View();

    private async Task SignIn(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Email),
            new(ClaimTypes.Email, user.Email)
        };
        if (user.IsAdmin) claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }
}
