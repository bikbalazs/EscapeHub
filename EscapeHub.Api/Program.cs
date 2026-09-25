using EscapeHub.Core.Entities;
using EscapeHub.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("EscapeHub") ??
    $"Data Source={Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "escapehub.db"))}";
builder.Services.AddDbContext<EscapeHubDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

var dataProtectionPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "EscapeHub", "DataProtectionKeys");
Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("EscapeHub");

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
    options.Cookie.Name = ".EscapeHub.AntiForgery";
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".EscapeHub.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EscapeHubDbContext>();
    await db.Database.EnsureCreatedAsync();
    await EnsureRoomDurationColumnAsync(db);
    await EnsureBookingParticipantCountColumnAsync(db);
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
    async Task EnsureConfiguredUserAsync(string? configuredEmail, string? password, bool isAdmin)
    {
        var email = configuredEmail?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) ||
            await db.Users.AnyAsync(user => user.Email == email)) return;
        var user = new User { Id = Guid.NewGuid(), Email = email, IsAdmin = isAdmin };
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }
    await EnsureConfiguredUserAsync(app.Configuration["EscapeHub:AdminEmail"], app.Configuration["EscapeHub:AdminPassword"], true);
    if (app.Environment.IsDevelopment())
        await EnsureConfiguredUserAsync(app.Configuration["EscapeHub:DemoUserEmail"], app.Configuration["EscapeHub:DemoUserPassword"], false);
}

static async Task EnsureRoomDurationColumnAsync(EscapeHubDbContext db)
{
    var connection = db.Database.GetDbConnection();
    var closeConnectionWhenDone = connection.State != ConnectionState.Open;
    if (closeConnectionWhenDone) await connection.OpenAsync();
    try
    {
        var hasColumn = false;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info('Rooms');";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (string.Equals(reader.GetString(1), "SolveDurationMinutes", StringComparison.OrdinalIgnoreCase))
                {
                    hasColumn = true;
                    break;
                }
            }
        }

        if (!hasColumn)
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Rooms\" ADD COLUMN \"SolveDurationMinutes\" INTEGER NOT NULL DEFAULT 60;");
        }
    }
    finally
    {
        if (closeConnectionWhenDone) await connection.CloseAsync();
    }
}

static async Task EnsureBookingParticipantCountColumnAsync(EscapeHubDbContext db)
{
    var connection = db.Database.GetDbConnection();
    var closeConnectionWhenDone = connection.State != ConnectionState.Open;
    if (closeConnectionWhenDone) await connection.OpenAsync();
    try
    {
        var hasColumn = false;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info('Bookings');";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (string.Equals(reader.GetString(1), "ParticipantCount", StringComparison.OrdinalIgnoreCase))
                {
                    hasColumn = true;
                    break;
                }
            }
        }

        if (!hasColumn)
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"Bookings\" ADD COLUMN \"ParticipantCount\" INTEGER NOT NULL DEFAULT 1;");
        }
    }
    finally
    {
        if (closeConnectionWhenDone) await connection.CloseAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
