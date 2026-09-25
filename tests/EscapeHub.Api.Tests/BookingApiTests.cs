using System.Security.Claims;
using EscapeHub.Api.Controllers;
using EscapeHub.Core.DTOs;
using EscapeHub.Core.Entities;
using EscapeHub.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EscapeHub.Api.Tests;

public sealed class BookingApiTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private EscapeHubDbContext _db = null!;
    private User _owner = null!;
    private User _otherUser = null!;
    private Room _room = null!;
    private BookingsApiController _controller = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EscapeHubDbContext>().UseSqlite(_connection).Options;
        _db = new EscapeHubDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        _owner = new User { Id = Guid.NewGuid(), Email = "owner@example.com", PasswordHash = "hash" };
        _otherUser = new User { Id = Guid.NewGuid(), Email = "other@example.com", PasswordHash = "hash" };
        _room = new Room { Name = "Próbaszoba", Capacity = 4 };
        _db.AddRange(_owner, _otherUser, _room);
        await _db.SaveChangesAsync();
        _controller = CreateController(_owner.Id);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Create_BooksAnAvailableSlotForTheSignedInUser()
    {
        var slot = await AddSlot(DateTime.UtcNow.AddDays(1));

        var result = await _controller.Create(new BookingCreateRequest { TimeSlotId = slot.Id, ParticipantCount = _room.Capacity });

        Assert.IsType<NoContentResult>(result);
        var booking = await _db.Bookings.SingleAsync();
        Assert.Equal(_owner.Id, booking.UserId);
        Assert.Equal(slot.Id, booking.TimeSlotId);
        Assert.Equal(_room.Capacity, booking.ParticipantCount);
        Assert.Null(booking.CancelledAtUtc);
    }

    [Fact]
    public async Task Create_AllowsTheMinimumParticipantCount()
    {
        var slot = await AddSlot(DateTime.UtcNow.AddDays(1));

        var result = await _controller.Create(new BookingCreateRequest
        {
            TimeSlotId = slot.Id
        });

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(2, (await _db.Bookings.SingleAsync()).ParticipantCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(5)]
    public async Task Create_RejectsParticipantCountOutsideRoomCapacity(int participantCount)
    {
        var slot = await AddSlot(DateTime.UtcNow.AddDays(1));

        var result = await _controller.Create(new BookingCreateRequest
        {
            TimeSlotId = slot.Id,
            ParticipantCount = participantCount
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await _db.Bookings.ToListAsync());
    }

    [Fact]
    public async Task Create_RejectsAnUnknownSlot()
    {
        var result = await _controller.Create(new BookingCreateRequest { TimeSlotId = 999 });

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Empty(await _db.Bookings.ToListAsync());
    }

    [Fact]
    public async Task Create_RejectsAnInactiveSlot()
    {
        var slot = await AddSlot(DateTime.UtcNow.AddDays(1), isActive: false);

        var result = await _controller.Create(new BookingCreateRequest { TimeSlotId = slot.Id });

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Empty(await _db.Bookings.ToListAsync());
    }

    [Fact]
    public async Task Create_RejectsAnExpiredSlot()
    {
        var slot = await AddSlot(DateTime.UtcNow.AddHours(-2));

        var result = await _controller.Create(new BookingCreateRequest { TimeSlotId = slot.Id });

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Empty(await _db.Bookings.ToListAsync());
    }

    [Fact]
    public async Task Create_RejectsASlotWithAnExistingActiveBooking()
    {
        var slot = await AddSlot(DateTime.UtcNow.AddDays(1));
        _db.Bookings.Add(NewBooking(slot, _otherUser));
        await _db.SaveChangesAsync();

        var result = await _controller.Create(new BookingCreateRequest { TimeSlotId = slot.Id });

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(await _db.Bookings.ToListAsync());
    }

    [Fact]
    public async Task Create_AllowsABookingAfterThePreviousOneWasCancelled()
    {
        var slot = await AddSlot(DateTime.UtcNow.AddDays(1));
        _db.Bookings.Add(NewBooking(slot, _otherUser, DateTime.UtcNow.AddMinutes(-5)));
        await _db.SaveChangesAsync();

        var result = await _controller.Create(new BookingCreateRequest { TimeSlotId = slot.Id });

        Assert.IsType<NoContentResult>(result);
        Assert.Single(await _db.Bookings.Where(booking => booking.CancelledAtUtc == null).ToListAsync());
    }

    [Fact]
    public async Task Cancel_CancelsTheSignedInUsersBooking()
    {
        var slot = await AddSlot(DateTime.UtcNow.AddDays(1));
        var booking = NewBooking(slot, _owner);
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        var result = await _controller.Cancel(booking.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.NotNull((await _db.Bookings.SingleAsync()).CancelledAtUtc);
    }

    [Fact]
    public async Task Cancel_DoesNotAllowCancellingAnotherUsersBooking()
    {
        var slot = await AddSlot(DateTime.UtcNow.AddDays(1));
        var booking = NewBooking(slot, _otherUser);
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        var result = await _controller.Cancel(booking.Id);

        Assert.IsType<NotFoundResult>(result);
        Assert.Null((await _db.Bookings.SingleAsync()).CancelledAtUtc);
    }

    [Fact]
    public async Task Cancel_ReturnsNotFoundForAlreadyCancelledBooking()
    {
        var slot = await AddSlot(DateTime.UtcNow.AddDays(1));
        var booking = NewBooking(slot, _owner, DateTime.UtcNow.AddMinutes(-5));
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        var result = await _controller.Cancel(booking.Id);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetMine_ReturnsOnlyTheSignedInUsersBookings()
    {
        var ownerSlot = await AddSlot(DateTime.UtcNow.AddDays(1));
        var otherSlot = await AddSlot(DateTime.UtcNow.AddDays(2));
        _db.Bookings.AddRange(NewBooking(ownerSlot, _owner), NewBooking(otherSlot, _otherUser));
        await _db.SaveChangesAsync();

        var result = await _controller.GetMine();

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var bookings = Assert.IsAssignableFrom<IReadOnlyList<BookingDto>>(response.Value);
        var booking = Assert.Single(bookings);
        Assert.Equal(_owner.Email, booking.CustomerEmail);
        Assert.Equal("Próbaszoba", booking.RoomName);
        Assert.Equal(1, booking.ParticipantCount);
    }

    private BookingsApiController CreateController(Guid userId)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test"))
        };
        return new BookingsApiController(_db)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private async Task<TimeSlot> AddSlot(DateTime startsAt, bool isActive = true)
    {
        var slot = new TimeSlot
        {
            RoomId = _room.Id,
            StartsAtUtc = startsAt,
            EndsAtUtc = startsAt.AddMinutes(90),
            IsActive = isActive
        };
        _db.TimeSlots.Add(slot);
        await _db.SaveChangesAsync();
        return slot;
    }

    private static Booking NewBooking(TimeSlot slot, User user, DateTime? cancelledAtUtc = null) => new()
    {
        TimeSlotId = slot.Id,
        UserId = user.Id,
        CreatedAtUtc = DateTime.UtcNow,
        CancelledAtUtc = cancelledAtUtc
    };
}
