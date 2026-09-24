using System.Security.Claims;
using System.Globalization;
using EscapeHub.Core.Entities;
using EscapeHub.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<EscapeHubDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("EscapeHub") ?? "Data Source=escapehub.db"));
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Denied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

var app = builder.Build();
var hungarian = new CultureInfo("hu-HU");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(hungarian),
    SupportedCultures = [hungarian],
    SupportedUICultures = [hungarian]
});
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EscapeHubDbContext>();
    await db.Database.EnsureCreatedAsync();

    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
    async Task EnsureConfiguredUserAsync(string? configuredEmail, string? password, bool isAdmin)
    {
        var email = configuredEmail?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) ||
            await db.Users.AnyAsync(user => user.Email == email))
        {
            return;
        }

        var user = new User { Id = Guid.NewGuid(), Email = email, IsAdmin = isAdmin };
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    await EnsureConfiguredUserAsync(
        app.Configuration["EscapeHub:AdminEmail"],
        app.Configuration["EscapeHub:AdminPassword"],
        isAdmin: true);

    if (app.Environment.IsDevelopment())
    {
        await EnsureConfiguredUserAsync(
            app.Configuration["EscapeHub:DemoUserEmail"],
            app.Configuration["EscapeHub:DemoUserPassword"],
            isAdmin: false);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(name: "default", pattern: "{controller=Rooms}/{action=Index}/{id?}");
app.Run();

