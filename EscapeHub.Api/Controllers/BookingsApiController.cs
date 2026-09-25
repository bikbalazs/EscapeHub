using System.Security.Claims;
using EscapeHub.Core.DTOs;
using EscapeHub.Core.Entities;
using EscapeHub.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EscapeHub.Api.Controllers;

[ApiController]
[Route("api/bookings")]
[Authorize]
public sealed class BookingsApiController(EscapeHubDbContext db) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<BookingDto>>> GetMine()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var bookings = await db.Bookings.AsNoTracking()
            .Include(booking => booking.TimeSlot).ThenInclude(slot => slot!.Room)
            .Include(booking => booking.User)
            .Where(booking => booking.UserId == userId)
            .OrderByDescending(booking => booking.TimeSlot!.StartsAtUtc)
            .ToListAsync();
        return Ok(bookings.Select(AdminBookingsController.ToDto).ToList());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BookingCreateRequest request)
    {
        var slot = await db.TimeSlots.Include(item => item.Room).Include(item => item.Bookings)
            .SingleOrDefaultAsync(item => item.Id == request.TimeSlotId);
        if (slot is null || !slot.IsActive || slot.EndsAtUtc <= DateTime.UtcNow ||
            slot.Bookings.Any(booking => booking.CancelledAtUtc is null))
        {
            return Conflict(new { message = "Ez az időpont már nem foglalható." });
        }
        var roomCapacity = slot.Room?.Capacity ?? 0;
        if (request.ParticipantCount < 2 || request.ParticipantCount > roomCapacity)
        {
            return BadRequest(new { message = $"A résztvevők száma 2 és {roomCapacity} fő között lehet." });
        }

        var booking = new Booking
        {
            TimeSlotId = slot.Id,
            UserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            ParticipantCount = request.ParticipantCount,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Bookings.Add(booking);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Ezt az időpontot közben más lefoglalta." });
        }

        return NoContent();
    }

    [HttpPost("{id:int}/cancel"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var booking = await db.Bookings.SingleOrDefaultAsync(item =>
            item.Id == id && item.UserId == userId && item.CancelledAtUtc == null);
        if (booking is null) return NotFound();

        booking.CancelledAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
