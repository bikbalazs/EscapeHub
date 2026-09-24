using EscapeHub.Core.DTOs;
using EscapeHub.Web.Models;
using EscapeHub.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace EscapeHub.Web.Controllers;

public sealed class AccountController(EscapeHubApiClient api) : Controller
{
    [HttpGet]
    public IActionResult Register() => View(new RegisterModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await api.PostAsync<object>("api/account/register", new AccountRequest { Email = model.Email, Password = model.Password });
        if (!result.Succeeded)
        {
            result.AddErrorsTo(ModelState);
            return View(model);
        }
        return RedirectToAction("Index", "Rooms");
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await api.PostAsync<object>("api/account/login", new LoginRequest { Email = model.Email, Password = model.Password });
        if (!result.Succeeded)
        {
            result.AddErrorsTo(ModelState);
            return View(model);
        }
        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction("Index", "Rooms");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await api.PostAsync<object>("api/account/logout");
        return RedirectToAction("Index", "Rooms");
    }

    [HttpGet]
    public IActionResult Denied() => View();
}
