using System.Security.Claims;
using EscapeHub.Core.Entities;
using EscapeHub.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EscapeHub.Web.Controllers;

public sealed class RoomsController(EscapeHubDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var now = DateTime.UtcNow;
        var rooms = await db.Rooms.Where(x => x.IsActive)
            .Include(x => x.TimeSlots.Where(s => s.IsActive && s.EndsAtUtc > now))
                .ThenInclude(x => x.Bookings)
            .OrderBy(x => x.Name).ToListAsync();
        return View(rooms);
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(int slotId)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var slot = await db.TimeSlots.Include(x => x.Bookings).SingleOrDefaultAsync(x => x.Id == slotId);
        if (slot is null || !slot.IsActive || slot.EndsAtUtc <= DateTime.UtcNow ||
            slot.Bookings.Any(x => x.CancelledAtUtc is null))
        {
            TempData["Message"] = "Ez az időpont már nem foglalható.";
            return RedirectToAction(nameof(Index));
        }
        db.Bookings.Add(new Booking { TimeSlotId = slotId, UserId = userId, CreatedAtUtc = DateTime.UtcNow });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            TempData["Message"] = "Ezt az időpontot közben más lefoglalta.";
            return RedirectToAction(nameof(Index));
        }
        TempData["Message"] = "A foglalás sikeresen létrejött.";
        return RedirectToAction(nameof(MyBookings));
    }

    [Authorize]
    public async Task<IActionResult> MyBookings()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var bookings = await db.Bookings.Include(x => x.TimeSlot).ThenInclude(x => x!.Room)
            .Where(x => x.UserId == userId).OrderByDescending(x => x.TimeSlot!.StartsAtUtc).ToListAsync();
        return View(bookings);
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var booking = await db.Bookings.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId && x.CancelledAtUtc == null);
        if (booking is null) return NotFound();
        booking.CancelledAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        TempData["Message"] = "A foglalást lemondtuk.";
        return RedirectToAction(nameof(MyBookings));
    }
}
