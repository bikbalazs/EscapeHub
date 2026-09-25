using EscapeHub.Api;
using EscapeHub.Core.Entities;
using EscapeHub.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EscapeHub.Api.Tests;

public sealed class DemoRoomSeederTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private EscapeHubDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EscapeHubDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new EscapeHubDbContext(options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task SeedAsync_CreatesAllFourHungarianRoomsWithTheirDurations()
    {
        var budapest = TimeZoneInfo.FindSystemTimeZoneById("Europe/Budapest");
        var nowUtc = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
        await DemoRoomSeeder.SeedAsync(_db, nowUtc, budapest);

        var rooms = await _db.Rooms.OrderBy(room => room.Name).ToListAsync();
        Assert.Equal(4, rooms.Count);
        Assert.Collection(rooms,
            room => AssertRoom(room, "Gyilkossági rejtély", 90),
            room => AssertRoom(room, "Idegen invázió", 120),
            room => AssertRoom(room, "Kastély", 90),
            room => AssertRoom(room, "Kazamata", 60));
        Assert.Contains("Ki e kastély falait elhagyni kívánja, a rejtély megfejtését lelje meg nyitjára.",
            rooms.Single(room => room.Name == "Kastély").Description);

        var slots = await _db.TimeSlots.Include(slot => slot.Room).ToListAsync();
        Assert.Equal(36, slots.Count);
        for (var dayOffset = 0; dayOffset < 3; dayOffset++)
        {
            var date = new DateTime(2026, 9, 24).AddDays(dayOffset);
            var dailySlots = slots.Where(slot =>
                TimeZoneInfo.ConvertTimeFromUtc(slot.StartsAtUtc, budapest).Date == date).ToList();
            Assert.Equal(12, dailySlots.Count);
            Assert.Equal(new TimeSpan(16, 0, 0), dailySlots.Min(slot =>
                TimeZoneInfo.ConvertTimeFromUtc(slot.StartsAtUtc, budapest).TimeOfDay));
            Assert.All(dailySlots, slot => Assert.True(
                TimeZoneInfo.ConvertTimeFromUtc(slot.EndsAtUtc, budapest).TimeOfDay <= new TimeSpan(22, 0, 0)));
        }

        var alienSlots = slots.Where(slot => slot.Room!.Name == "Idegen invázió").ToList();
        Assert.Equal(2, alienSlots.Count / 3);
        Assert.Equal(3, alienSlots.Count(slot =>
            TimeZoneInfo.ConvertTimeFromUtc(slot.EndsAtUtc, budapest).TimeOfDay == new TimeSpan(18, 30, 0)));
        Assert.Equal(3, alienSlots.Count(slot =>
            TimeZoneInfo.ConvertTimeFromUtc(slot.EndsAtUtc, budapest).TimeOfDay == new TimeSpan(21, 0, 0)));
    }

    [Fact]
    public async Task SeedAsync_IsIdempotentAndDoesNotOverwriteAnExistingRoom()
    {
        _db.Rooms.Add(new Room
        {
            Name = "Kazamata",
            Description = "Saját leírás",
            Capacity = 8,
            SolveDurationMinutes = 60,
            IsActive = false
        });
        await _db.SaveChangesAsync();

        var nowUtc = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
        var budapest = TimeZoneInfo.FindSystemTimeZoneById("Europe/Budapest");
        await DemoRoomSeeder.SeedAsync(_db, nowUtc, budapest);
        var slotCount = await _db.TimeSlots.CountAsync();
        await DemoRoomSeeder.SeedAsync(_db, nowUtc, budapest);

        var rooms = await _db.Rooms.ToListAsync();
        Assert.Equal(4, rooms.Count);
        var existingRoom = rooms.Single(room => room.Name == "Kazamata");
        Assert.Equal("Saját leírás", existingRoom.Description);
        Assert.Equal(8, existingRoom.Capacity);
        Assert.False(existingRoom.IsActive);
        Assert.Equal(slotCount, await _db.TimeSlots.CountAsync());
    }

    private static void AssertRoom(Room room, string expectedName, int expectedDuration)
    {
        Assert.Equal(expectedName, room.Name);
        Assert.Equal(expectedDuration, room.SolveDurationMinutes);
        Assert.Equal(6, room.Capacity);
        Assert.True(room.IsActive);
        Assert.False(string.IsNullOrWhiteSpace(room.Description));
    }
}
