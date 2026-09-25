using EscapeHub.Api.Controllers;
using EscapeHub.Core.DTOs;
using EscapeHub.Core.Entities;
using EscapeHub.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace EscapeHub.Api.Tests;

public sealed class AccountApiTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly RecordingAuthenticationService _authentication = new();
    private ServiceProvider _services = null!;
    private EscapeHubDbContext _db = null!;
    private PasswordHasher<User> _passwordHasher = null!;
    private AccountApiController _controller = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EscapeHubDbContext>().UseSqlite(_connection).Options;
        _db = new EscapeHubDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _passwordHasher = new PasswordHasher<User>();
        _services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(_authentication)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = _services };
        _controller = new AccountApiController(_db, _passwordHasher)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
        await _services.DisposeAsync();
    }

    [Fact]
    public async Task Register_NormalizesEmail_HashesPassword_AndSignsInUser()
    {
        var result = await _controller.Register(new AccountRequest
        {
            Email = "  PERSON@Example.com ",
            Password = "CorrectHorseBattery1!"
        });

        Assert.IsType<NoContentResult>(result);
        var user = await _db.Users.SingleAsync();
        Assert.Equal("person@example.com", user.Email);
        Assert.NotEqual("CorrectHorseBattery1!", user.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success,
            _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, "CorrectHorseBattery1!"));
        Assert.Equal(user.Id.ToString(), _authentication.SignedInPrincipal!.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    [Fact]
    public async Task Register_RejectsEmailThatAlreadyExists()
    {
        _db.Users.Add(NewUser("existing@example.com", "ExistingPassword1!"));
        await _db.SaveChangesAsync();

        var result = await _controller.Register(new AccountRequest
        {
            Email = " EXISTING@example.com ",
            Password = "AnotherPassword1!"
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Single(await _db.Users.ToListAsync());
        Assert.Null(_authentication.SignedInPrincipal);
    }

    [Fact]
    public async Task Login_AcceptsCorrectPasswordAndNormalizesEmail()
    {
        var user = NewUser("person@example.com", "CorrectHorseBattery1!");
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var result = await _controller.Login(new LoginRequest
        {
            Email = " PERSON@EXAMPLE.COM ",
            Password = "CorrectHorseBattery1!"
        });

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(user.Id.ToString(), _authentication.SignedInPrincipal!.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    [Theory]
    [InlineData("person@example.com", "WrongPassword1!")]
    [InlineData("missing@example.com", "CorrectHorseBattery1!")]
    public async Task Login_RejectsUnknownUserOrIncorrectPassword(string email, string password)
    {
        _db.Users.Add(NewUser("person@example.com", "CorrectHorseBattery1!"));
        await _db.SaveChangesAsync();

        var result = await _controller.Login(new LoginRequest { Email = email, Password = password });

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Null(_authentication.SignedInPrincipal);
    }

    [Fact]
    public async Task LogoutSignsOutCurrentSession()
    {
        var result = await _controller.Logout();

        Assert.IsType<NoContentResult>(result);
        Assert.True(_authentication.SignOutWasCalled);
    }

    private User NewUser(string email, string password)
    {
        var user = new User { Id = Guid.NewGuid(), Email = email };
        user.PasswordHash = _passwordHasher.HashPassword(user, password);
        return user;
    }

    private sealed class RecordingAuthenticationService : IAuthenticationService
    {
        public ClaimsPrincipal? SignedInPrincipal { get; private set; }
        public bool SignOutWasCalled { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            SignedInPrincipal = principal;
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignOutWasCalled = true;
            return Task.CompletedTask;
        }
    }
}
