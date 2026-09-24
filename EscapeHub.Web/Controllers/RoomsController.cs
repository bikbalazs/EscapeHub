using EscapeHub.Core.DTOs;
using EscapeHub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EscapeHub.Web.Controllers;

public sealed class RoomsController(EscapeHubApiClient api) : Controller
{
    public async Task<IActionResult> Index()
    {
        var result = await api.GetAsync<IReadOnlyList<RoomDto>>("api/rooms");
        if (!result.Succeeded)
        {
            TempData["Message"] = "A szobák betöltése nem sikerült. Ellenőrizze, hogy fut-e az API.";
            return View(Array.Empty<RoomDto>());
        }
        return View(result.Value ?? Array.Empty<RoomDto>());
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(int slotId)
    {
        var result = await api.PostAsync<object>("api/bookings", new BookingCreateRequest { TimeSlotId = slotId });
        TempData["Message"] = result.Succeeded
            ? "A foglalás sikeresen létrejött."
            : result.Message ?? "Ez az időpont már nem foglalható.";
        return RedirectToAction(result.Succeeded ? nameof(MyBookings) : nameof(Index));
    }

    [Authorize]
    public async Task<IActionResult> MyBookings()
    {
        var result = await api.GetAsync<IReadOnlyList<BookingDto>>("api/bookings/mine");
        if (!result.Succeeded)
        {
            TempData["Message"] = "A foglalások betöltése nem sikerült.";
            return View(Array.Empty<BookingDto>());
        }
        return View(result.Value ?? Array.Empty<BookingDto>());
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var result = await api.PostAsync<object>($"api/bookings/{id}/cancel");
        if (result.StatusCode == System.Net.HttpStatusCode.NotFound) return NotFound();
        TempData["Message"] = result.Succeeded ? "A foglalást lemondtuk." : result.Message ?? "A foglalást nem sikerült lemondani.";
        return RedirectToAction(nameof(MyBookings));
    }
}
