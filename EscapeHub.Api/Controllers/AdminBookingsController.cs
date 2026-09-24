using EscapeHub.Core.Entities;
using EscapeHub.Core.DTOs;
using EscapeHub.Infrastructure.Data;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EscapeHub.Api.Controllers;

[ApiController]
[Route("api/admin/bookings")]
[Authorize(Roles = "Admin")]
public sealed class AdminBookingsController(EscapeHubDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BookingDto>>> GetBookings()
    {
        var bookings = await db.Bookings.AsNoTracking()
            .Include(booking => booking.TimeSlot).ThenInclude(slot => slot!.Room)
            .Include(booking => booking.User)
            .OrderBy(booking => booking.CancelledAtUtc != null)
            .ThenBy(booking => booking.TimeSlot!.StartsAtUtc)
            .ToListAsync();

        return Ok(bookings.Select(ToDto).ToList());
    }

    [HttpPost("{id:int}/cancel"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelBooking(int id)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(item => item.Id == id);
        if (booking is null) return NotFound();
        if (booking.CancelledAtUtc is not null)
        {
            return Conflict(new { message = "Ezt a foglalást korábban már lemondták." });
        }

        booking.CancelledAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    internal static BookingDto ToDto(Booking booking) => new(
        booking.Id,
        booking.TimeSlotId,
        booking.TimeSlot!.Room!.Name,
        booking.User!.Email,
        DateTime.SpecifyKind(booking.TimeSlot.StartsAtUtc, DateTimeKind.Utc),
        DateTime.SpecifyKind(booking.TimeSlot.EndsAtUtc, DateTimeKind.Utc),
        booking.CancelledAtUtc is DateTime cancelledAt
            ? DateTime.SpecifyKind(cancelledAt, DateTimeKind.Utc)
            : null);
}
