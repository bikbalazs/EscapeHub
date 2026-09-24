using EscapeHub.Core.Entities;
using EscapeHub.Infrastructure.Data;
using EscapeHub.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EscapeHub.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class AdminController(EscapeHubDbContext db, IPasswordHasher<User> passwordHasher) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.Users = await db.Users.AsNoTracking().OrderBy(x => x.Email).ToListAsync();
        return View(await db.Rooms.Include(x => x.TimeSlots).ThenInclude(x => x.Bookings)
            .OrderBy(x => x.Name).ToListAsync());
    }

    [HttpGet]
    public IActionResult Room(int? id) => View("Room", id is null ? new RoomFormModel() :
        db.Rooms.Where(x => x.Id == id).Select(x => new RoomFormModel { Id = x.Id, Name = x.Name, Description = x.Description, Capacity = x.Capacity }).FirstOrDefault() ?? new RoomFormModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRoom(RoomFormModel model)
    {
        if (!ModelState.IsValid) return View("Room", model);
        if (model.Id is null) db.Rooms.Add(new Room { Name = model.Name.Trim(), Description = model.Description.Trim(), Capacity = model.Capacity });
        else
        {
            var room = await db.Rooms.FindAsync(model.Id);
            if (room is null) return NotFound();
            room.Name = model.Name.Trim(); room.Description = model.Description.Trim(); room.Capacity = model.Capacity;
        }
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateRoom(int id)
    {
        var room = await db.Rooms.Include(x => x.TimeSlots).SingleOrDefaultAsync(x => x.Id == id);
        if (room is null) return NotFound();
        room.IsActive = false;
        foreach (var slot in room.TimeSlots) slot.IsActive = false;
        await db.SaveChangesAsync();
        TempData["Message"] = "A szobát inaktiváltuk; a korábbi foglalások megmaradtak.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ActivateRoom(int id)
    {
        var room = await db.Rooms.FindAsync(id);
        if (room is null) return NotFound();
        room.IsActive = true;
        await db.SaveChangesAsync();
        TempData["Message"] = "A szobát újra aktiváltuk. Az időpontokat külön kell aktiválni.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Slot(int roomId, int? id)
    {
        var room = await db.Rooms.FindAsync(roomId);
        if (room is null) return NotFound();
        ViewBag.RoomName = room.Name; ViewBag.RoomId = room.Id;
        var model = id is null ? new TimeSlotFormModel { RoomId = roomId, StartsAtUtc = DateTime.UtcNow.AddDays(1), EndsAtUtc = DateTime.UtcNow.AddDays(1).AddHours(1) }
            : await db.TimeSlots.Where(x => x.Id == id && x.RoomId == roomId).Select(x => new TimeSlotFormModel { Id = x.Id, RoomId = x.RoomId, StartsAtUtc = x.StartsAtUtc, EndsAtUtc = x.EndsAtUtc }).FirstOrDefaultAsync();
        return model is null ? NotFound() : View("Slot", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSlot(TimeSlotFormModel model)
    {
        var room = await db.Rooms.FindAsync(model.RoomId);
        if (room is null) return NotFound();
        if (!room.IsActive) return BadRequest();
        ViewBag.RoomName = room.Name; ViewBag.RoomId = room.Id;
        model.StartsAtUtc = DateTime.SpecifyKind(model.StartsAtUtc, DateTimeKind.Utc);
        model.EndsAtUtc = DateTime.SpecifyKind(model.EndsAtUtc, DateTimeKind.Utc);
        if (model.EndsAtUtc <= model.StartsAtUtc || model.StartsAtUtc <= DateTime.UtcNow)
            ModelState.AddModelError(string.Empty, "A kezdés legyen a jövőben, a befejezés pedig a kezdés után.");
        var slot = model.Id is null ? null : await db.TimeSlots.Include(x => x.Bookings).SingleOrDefaultAsync(x => x.Id == model.Id && x.RoomId == model.RoomId);
        if (model.Id is not null && slot is null) return NotFound();
        if (slot?.Bookings.Any(x => x.CancelledAtUtc is null) == true &&
            (slot.StartsAtUtc != model.StartsAtUtc || slot.EndsAtUtc != model.EndsAtUtc))
            ModelState.AddModelError(string.Empty, "Foglalt időpont ideje nem módosítható. Előbb mondja le a foglalást.");
        if (!ModelState.IsValid) return View("Slot", model);
        if (slot is null) db.TimeSlots.Add(new TimeSlot { RoomId = model.RoomId, StartsAtUtc = model.StartsAtUtc, EndsAtUtc = model.EndsAtUtc });
        else { slot.StartsAtUtc = model.StartsAtUtc; slot.EndsAtUtc = model.EndsAtUtc; slot.IsActive = true; }
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { ModelState.AddModelError(string.Empty, "Már létezik időpont ehhez a szobához ezzel a kezdéssel."); return View("Slot", model); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateSlot(int id)
    {
        var slot = await db.TimeSlots.FindAsync(id);
        if (slot is null) return NotFound();
        if (await db.Bookings.AnyAsync(x => x.TimeSlotId == id && x.CancelledAtUtc == null))
        {
            TempData["Message"] = "Foglalt időpont nem törölhető. Előbb mondja le a foglalást.";
            return RedirectToAction(nameof(Index));
        }
        slot.IsActive = false;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Users() => View("User", new AdminUserModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveUser(AdminUserModel model)
    {
        if (!ModelState.IsValid) return View("User", model);
        var email = model.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email))
        {
            ModelState.AddModelError(nameof(model.Email), "Ezzel az e-mail-címmel már létezik felhasználó.");
            return View("User", model);
        }
        var user = new User { Id = Guid.NewGuid(), Email = email, IsAdmin = model.IsAdmin };
        user.PasswordHash = passwordHasher.HashPassword(user, model.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        TempData["Message"] = "A felhasználó létrejött.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAdmin(Guid id, bool isAdmin)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        user.IsAdmin = isAdmin;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
