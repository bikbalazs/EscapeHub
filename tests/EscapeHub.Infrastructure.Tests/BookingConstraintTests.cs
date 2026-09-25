using EscapeHub.Core.Entities;
using EscapeHub.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EscapeHub.Infrastructure.Tests;

public sealed class BookingConstraintTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private EscapeHubDbContext _db = null!;
    private TimeSlot _slot = null!;
    private User _user = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<EscapeHubDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new EscapeHubDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        var room = new Room { Name = "Próbaszoba", Capacity = 4 };
        _user = new User { Id = Guid.NewGuid(), Email = "teszt@example.com", PasswordHash = "test-hash" };
        _slot = new TimeSlot
        {
            Room = room,
            StartsAtUtc = DateTime.UtcNow.AddDays(2),
            EndsAtUtc = DateTime.UtcNow.AddDays(2).AddMinutes(90)
        };
        _db.AddRange(room, _user, _slot);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Database_RejectsTwoActiveBookingsForOneSlot()
    {
        _db.Bookings.Add(NewBooking(cancelledAtUtc: null));
        await _db.SaveChangesAsync();

        _db.Bookings.Add(NewBooking(cancelledAtUtc: null));

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task Database_AllowsNewBookingAfterPreviousBookingWasCancelled()
    {
        var previousBooking = NewBooking(cancelledAtUtc: DateTime.UtcNow);
        var replacementBooking = NewBooking(cancelledAtUtc: null);
        _db.Bookings.AddRange(previousBooking, replacementBooking);

        await _db.SaveChangesAsync();

        Assert.Equal(2, await _db.Bookings.CountAsync());
        Assert.Single(await _db.Bookings.Where(booking => booking.CancelledAtUtc == null).ToListAsync());
    }

    private Booking NewBooking(DateTime? cancelledAtUtc) => new()
    {
        TimeSlotId = _slot.Id,
        UserId = _user.Id,
        CreatedAtUtc = DateTime.UtcNow,
        CancelledAtUtc = cancelledAtUtc
    };
}
