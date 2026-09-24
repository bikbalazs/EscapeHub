using EscapeHub.Core.DTOs;
using EscapeHub.Core.Entities;
using EscapeHub.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EscapeHub.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public sealed class AdminApiController(
    EscapeHubDbContext db,
    IPasswordHasher<User> passwordHasher) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<AdminOverviewDto>> GetOverview()
    {
        var rooms = await db.Rooms.AsNoTracking()
            .Include(room => room.TimeSlots).ThenInclude(slot => slot.Bookings)
            .OrderBy(room => room.Name)
            .ToListAsync();
        var users = await db.Users.AsNoTracking().OrderBy(user => user.Email)
            .Select(user => new UserDto(user.Id, user.Email, user.IsAdmin))
            .ToListAsync();

        return Ok(new AdminOverviewDto(rooms.Select(RoomsApiController.ToRoomDto).ToList(), users));
    }

    [HttpGet("rooms/{id:int}")]
    public async Task<ActionResult<RoomDto>> GetRoom(int id)
    {
        var room = await db.Rooms.AsNoTracking()
            .Include(item => item.TimeSlots).ThenInclude(slot => slot.Bookings)
            .SingleOrDefaultAsync(item => item.Id == id);
        return room is null ? NotFound() : Ok(RoomsApiController.ToRoomDto(room));
    }

    [HttpGet("rooms/{roomId:int}/slots/{slotId:int}")]
    public async Task<ActionResult<TimeSlotDto>> GetSlot(int roomId, int slotId)
    {
        var slot = await db.TimeSlots.AsNoTracking().Include(item => item.Bookings)
            .SingleOrDefaultAsync(item => item.Id == slotId && item.RoomId == roomId);
        return slot is null ? NotFound() : Ok(ToTimeSlotDto(slot));
    }

    [HttpPost("rooms"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRoom(RoomSaveRequest request)
    {
        var room = new Room
        {
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Capacity = request.Capacity,
            SolveDurationMinutes = request.SolveDurationMinutes
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetRoom), new { id = room.Id }, RoomsApiController.ToRoomDto(room));
    }

    [HttpPut("rooms/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRoom(int id, RoomSaveRequest request)
    {
        var room = await db.Rooms.FindAsync(id);
        if (room is null) return NotFound();
        room.Name = request.Name.Trim();
        room.Description = request.Description.Trim();
        room.Capacity = request.Capacity;
        room.SolveDurationMinutes = request.SolveDurationMinutes;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("rooms/{id:int}/deactivate"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateRoom(int id)
    {
        var room = await db.Rooms.Include(item => item.TimeSlots)
            .SingleOrDefaultAsync(item => item.Id == id);
        if (room is null) return NotFound();

        var hasActiveBookings = await db.TimeSlots.Where(slot => slot.RoomId == id)
            .SelectMany(slot => slot.Bookings)
            .AnyAsync(booking => booking.CancelledAtUtc == null);
        if (hasActiveBookings)
        {
            return Conflict(new { message = "A szoba nem inaktiválható, mert aktív foglalás tartozik hozzá. Előbb mondja le a foglalást." });
        }

        room.IsActive = false;
        foreach (var slot in room.TimeSlots) slot.IsActive = false;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("rooms/{id:int}/activate"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ActivateRoom(int id)
    {
        var room = await db.Rooms.FindAsync(id);
        if (room is null) return NotFound();
        room.IsActive = true;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("slots"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSlot(TimeSlotSaveRequest request)
    {
        var (room, validation) = await ValidateSlotRequest(request);
        if (validation is not null) return validation;

        var slot = new TimeSlot
        {
            RoomId = request.RoomId,
            StartsAtUtc = AsUtc(request.StartsAtUtc),
            EndsAtUtc = CalculateEndTime(AsUtc(request.StartsAtUtc), room!.SolveDurationMinutes)
        };
        db.TimeSlots.Add(slot);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Már létezik időpont ehhez a szobához ezzel a kezdéssel." });
        }

        return CreatedAtAction(nameof(GetSlot), new { roomId = slot.RoomId, slotId = slot.Id }, ToTimeSlotDto(slot));
    }

    [HttpPut("slots/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSlot(int id, TimeSlotSaveRequest request)
    {
        var (room, validation) = await ValidateSlotRequest(request);
        if (validation is not null) return validation;

        var slot = await db.TimeSlots.Include(item => item.Bookings)
            .SingleOrDefaultAsync(item => item.Id == id && item.RoomId == request.RoomId);
        if (slot is null) return NotFound();

        var start = AsUtc(request.StartsAtUtc);
        var end = CalculateEndTime(start, room!.SolveDurationMinutes);
        if (slot.Bookings.Any(booking => booking.CancelledAtUtc is null) &&
            (slot.StartsAtUtc != start || slot.EndsAtUtc != end))
        {
            return Conflict(new { message = "Foglalt időpont ideje nem módosítható. Előbb mondja le a foglalást." });
        }

        slot.StartsAtUtc = start;
        slot.EndsAtUtc = end;
        slot.IsActive = true;
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Már létezik időpont ehhez a szobához ezzel a kezdéssel." });
        }

        return NoContent();
    }

    [HttpPost("slots/{id:int}/deactivate"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateSlot(int id)
    {
        var slot = await db.TimeSlots.FindAsync(id);
        if (slot is null) return NotFound();
        if (await db.Bookings.AnyAsync(booking =>
                booking.TimeSlotId == id && booking.CancelledAtUtc == null))
        {
            return Conflict(new { message = "Foglalt időpont nem inaktiválható. Előbb mondja le a foglalást." });
        }

        slot.IsActive = false;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("users"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(AdminUserCreateRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(user => user.Email == email))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.Email)] = ["Ezzel az e-mail-címmel már létezik felhasználó."]
            }));
        }

        var user = new User { Id = Guid.NewGuid(), Email = email, IsAdmin = request.IsAdmin };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.Email)] = ["Ezzel az e-mail-címmel már létezik felhasználó."]
            }));
        }

        return NoContent();
    }

    [HttpPost("users/{id:guid}/role"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SetUserRole(Guid id, SetAdminRequest request)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        user.IsAdmin = request.IsAdmin;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<(Room? Room, IActionResult? Error)> ValidateSlotRequest(TimeSlotSaveRequest request)
    {
        var room = await db.Rooms.FindAsync(request.RoomId);
        if (room is null) return (null, NotFound(new { message = "A szoba nem található." }));
        if (!room.IsActive) return (null, Conflict(new { message = "Inaktív szobához nem adható meg időpont." }));

        var start = AsUtc(request.StartsAtUtc);
        if (start <= DateTime.UtcNow)
        {
            return (null, BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [string.Empty] = ["A kezdés legyen a jövőben."]
            })));
        }

        return (room, null);
    }

    private static TimeSlotDto ToTimeSlotDto(TimeSlot slot) =>
        new(slot.Id, slot.RoomId, AsUtc(slot.StartsAtUtc), AsUtc(slot.EndsAtUtc),
            slot.IsActive, slot.Bookings.Any(booking => booking.CancelledAtUtc is null));

    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime CalculateEndTime(DateTime startsAtUtc, int solveDurationMinutes) =>
        startsAtUtc.AddMinutes(solveDurationMinutes + 30);
}
