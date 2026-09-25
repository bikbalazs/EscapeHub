using EscapeHub.Api.Controllers;
using EscapeHub.Core.DTOs;
using EscapeHub.Core.Entities;
using EscapeHub.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EscapeHub.Api.Tests;

public sealed class RoomsApiTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private EscapeHubDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EscapeHubDbContext>().UseSqlite(_connection).Options;
        _db = new EscapeHubDbContext(options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task GetAvailableRooms_ExcludesInactiveRoomsAndUnavailableSlots()
    {
        var activeRoom = new Room { Name = "Aktív", Capacity = 4 };
        var inactiveRoom = new Room { Name = "Inaktív", Capacity = 4, IsActive = false };
        _db.Rooms.AddRange(activeRoom, inactiveRoom);
        await _db.SaveChangesAsync();

        _db.TimeSlots.AddRange(
            NewSlot(activeRoom, DateTime.UtcNow.AddDays(1)),
            NewSlot(activeRoom, DateTime.UtcNow.AddHours(-2)),
            NewSlot(activeRoom, DateTime.UtcNow.AddDays(2), isActive: false),
            NewSlot(inactiveRoom, DateTime.UtcNow.AddDays(1)));
        await _db.SaveChangesAsync();

        var result = await new RoomsApiController(_db).GetAvailableRooms();

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var rooms = Assert.IsAssignableFrom<IReadOnlyList<RoomDto>>(response.Value);
        var room = Assert.Single(rooms);
        Assert.Equal("Aktív", room.Name);
        Assert.Single(room.TimeSlots);
    }

    [Fact]
    public async Task GetAvailableRooms_ReportsWhetherEachSlotIsBooked()
    {
        var room = new Room { Name = "Foglalható", Capacity = 4 };
        var user = new User { Id = Guid.NewGuid(), Email = "user@example.com", PasswordHash = "hash" };
        var slot = NewSlot(room, DateTime.UtcNow.AddDays(1));
        _db.AddRange(room, user, slot);
        await _db.SaveChangesAsync();
        _db.Bookings.Add(new Booking
        {
            TimeSlotId = slot.Id,
            UserId = user.Id,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var result = await new RoomsApiController(_db).GetAvailableRooms();

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var rooms = Assert.IsAssignableFrom<IReadOnlyList<RoomDto>>(response.Value);
        Assert.True(Assert.Single(Assert.Single(rooms).TimeSlots).HasActiveBooking);
    }

    private static TimeSlot NewSlot(Room room, DateTime startsAt, bool isActive = true) => new()
    {
        Room = room,
        StartsAtUtc = startsAt,
        EndsAtUtc = startsAt.AddMinutes(90),
        IsActive = isActive
    };
}
