using System.ComponentModel.DataAnnotations;
using EscapeHub.Api.Controllers;
using EscapeHub.Core.DTOs;
using EscapeHub.Core.Entities;
using EscapeHub.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EscapeHub.Api.Tests;

public sealed class AdminSlotValidationTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private EscapeHubDbContext _db = null!;
    private AdminApiController _controller = null!;
    private Room _room = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EscapeHubDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new EscapeHubDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        _room = new Room { Name = "Próbaszoba", Capacity = 4, SolveDurationMinutes = 60 };
        _db.Rooms.Add(_room);
        await _db.SaveChangesAsync();

        _controller = new AdminApiController(_db, new PasswordHasher<User>())
        {
            ControllerContext = new ControllerContext()
        };
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Theory]
    [InlineData(60, 90)]
    [InlineData(90, 120)]
    [InlineData(120, 150)]
    public async Task CreateSlot_SetsEndToSolveDurationPlusSetupTime(int solveMinutes, int expectedDurationMinutes)
    {
        _room.SolveDurationMinutes = solveMinutes;
        await _db.SaveChangesAsync();
        var startsAt = DateTime.UtcNow.AddDays(2);

        var result = await _controller.CreateSlot(new TimeSlotSaveRequest
        {
            RoomId = _room.Id,
            StartsAtUtc = startsAt
        });

        Assert.IsType<CreatedAtActionResult>(result);
        var savedSlot = await _db.TimeSlots.SingleAsync();
        Assert.Equal(startsAt.AddMinutes(expectedDurationMinutes), savedSlot.EndsAtUtc);
    }

    [Fact]
    public async Task CreateSlot_RejectsOverlapWithSetupTimeOfAnotherSlot()
    {
        var firstStart = DateTime.UtcNow.AddDays(2).Date.AddHours(10);
        await AddSlot(firstStart);

        var result = await _controller.CreateSlot(new TimeSlotSaveRequest
        {
            RoomId = _room.Id,
            StartsAtUtc = firstStart.AddMinutes(75)
        });

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Contains("ütközik", conflict.Value!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Single(await _db.TimeSlots.ToListAsync());
    }

    [Fact]
    public async Task CreateSlot_AllowsSlotStartingExactlyWhenPreviousOneEnds()
    {
        var firstStart = DateTime.UtcNow.AddDays(2).Date.AddHours(10);
        var firstSlot = await AddSlot(firstStart);

        var result = await _controller.CreateSlot(new TimeSlotSaveRequest
        {
            RoomId = _room.Id,
            StartsAtUtc = firstSlot.EndsAtUtc
        });

        Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(2, await _db.TimeSlots.CountAsync());
    }

    [Fact]
    public async Task UpdateSlot_DoesNotOverlapItself()
    {
        var slot = await AddSlot(DateTime.UtcNow.AddDays(2));

        var result = await _controller.UpdateSlot(slot.Id, new TimeSlotSaveRequest
        {
            RoomId = _room.Id,
            StartsAtUtc = slot.StartsAtUtc
        });

        Assert.IsType<NoContentResult>(result);
        Assert.Single(await _db.TimeSlots.ToListAsync());
    }

    [Fact]
    public async Task UpdateSlot_RejectsChangingAnActivelyBookedTime()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "booker@example.com", PasswordHash = "hash" };
        var slot = await AddSlot(DateTime.UtcNow.AddDays(2));
        _db.Users.Add(user);
        _db.Bookings.Add(new Booking
        {
            TimeSlotId = slot.Id,
            UserId = user.Id,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var result = await _controller.UpdateSlot(slot.Id, new TimeSlotSaveRequest
        {
            RoomId = _room.Id,
            StartsAtUtc = slot.StartsAtUtc.AddHours(1)
        });

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(slot.StartsAtUtc, (await _db.TimeSlots.SingleAsync()).StartsAtUtc);
    }

    [Fact]
    public async Task DeactivateRoom_RejectsRoomWithAnActiveBooking()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "booker@example.com", PasswordHash = "hash" };
        var slot = await AddSlot(DateTime.UtcNow.AddDays(2));
        _db.Users.Add(user);
        _db.Bookings.Add(new Booking
        {
            TimeSlotId = slot.Id,
            UserId = user.Id,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var result = await _controller.DeactivateRoom(_room.Id);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.True((await _db.Rooms.SingleAsync()).IsActive);
        Assert.True((await _db.TimeSlots.SingleAsync()).IsActive);
    }

    [Fact]
    public async Task DeactivateRoom_DeactivatesRoomAndItsSlotsWhenNoActiveBookingsExist()
    {
        await AddSlot(DateTime.UtcNow.AddDays(2));
        await AddSlot(DateTime.UtcNow.AddDays(3));

        var result = await _controller.DeactivateRoom(_room.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.False((await _db.Rooms.SingleAsync()).IsActive);
        Assert.All(await _db.TimeSlots.ToListAsync(), slot => Assert.False(slot.IsActive));
    }

    [Fact]
    public async Task DeactivateSlot_RejectsAnActivelyBookedSlot()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "booker@example.com", PasswordHash = "hash" };
        var slot = await AddSlot(DateTime.UtcNow.AddDays(2));
        _db.Users.Add(user);
        _db.Bookings.Add(new Booking
        {
            TimeSlotId = slot.Id,
            UserId = user.Id,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var result = await _controller.DeactivateSlot(slot.Id);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.True((await _db.TimeSlots.SingleAsync()).IsActive);
    }

    [Theory]
    [InlineData(60, true)]
    [InlineData(90, true)]
    [InlineData(120, true)]
    [InlineData(45, false)]
    [InlineData(240, false)]
    public void RoomSaveRequest_AllowsOnlySupportedSolveDurations(int solveMinutes, bool expectedValid)
    {
        var request = new RoomSaveRequest
        {
            Name = "Próbaszoba",
            Capacity = 4,
            SolveDurationMinutes = solveMinutes
        };
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        Assert.Equal(expectedValid, isValid);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public void RoomSaveRequest_RequiresRoomCapacityOfAtLeastTwo(int capacity, bool expectedValid)
    {
        var request = new RoomSaveRequest
        {
            Name = "Próbaszoba",
            Capacity = capacity,
            SolveDurationMinutes = 60
        };
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        Assert.Equal(expectedValid, isValid);
    }

    private async Task<TimeSlot> AddSlot(DateTime startsAt)
    {
        var slot = new TimeSlot
        {
            RoomId = _room.Id,
            StartsAtUtc = startsAt,
            EndsAtUtc = startsAt.AddMinutes(_room.SolveDurationMinutes + 30)
        };
        _db.TimeSlots.Add(slot);
        await _db.SaveChangesAsync();
        return slot;
    }
}
