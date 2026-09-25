using EscapeHub.Core.DTOs;
using EscapeHub.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EscapeHub.Api.Controllers;

[ApiController]
[Route("api/rooms")]
public sealed class RoomsApiController(EscapeHubDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoomDto>>> GetAvailableRooms()
    {
        var now = DateTime.UtcNow;
        var rooms = await db.Rooms.AsNoTracking()
            .Where(room => room.IsActive)
            .Include(room => room.TimeSlots.Where(slot => slot.IsActive && slot.EndsAtUtc > now))
                .ThenInclude(slot => slot.Bookings)
            .OrderBy(room => room.Name)
            .ToListAsync();

        return Ok(rooms.Select(ToRoomDto).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RoomDto>> GetAvailableRoom(int id)
    {
        var now = DateTime.UtcNow;
        var room = await db.Rooms.AsNoTracking()
            .Where(item => item.Id == id && item.IsActive)
            .Include(item => item.TimeSlots.Where(slot => slot.IsActive && slot.EndsAtUtc > now))
                .ThenInclude(slot => slot.Bookings)
            .SingleOrDefaultAsync();

        return room is null ? NotFound() : Ok(ToRoomDto(room));
    }

    internal static RoomDto ToRoomDto(EscapeHub.Core.Entities.Room room) =>
        new(room.Id, room.Name, room.Description, room.Capacity, room.SolveDurationMinutes, room.IsActive,
            room.TimeSlots.OrderBy(slot => slot.StartsAtUtc).Select(slot =>
                new TimeSlotDto(
                    slot.Id,
                    slot.RoomId,
                    DateTime.SpecifyKind(slot.StartsAtUtc, DateTimeKind.Utc),
                    DateTime.SpecifyKind(slot.EndsAtUtc, DateTimeKind.Utc),
                    slot.IsActive,
                    slot.Bookings.Any(booking => booking.CancelledAtUtc is null)))
            .ToList());
}
