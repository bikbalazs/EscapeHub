using EscapeHub.Core.DTOs;
using EscapeHub.Web.Models;
using EscapeHub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace EscapeHub.Web.Controllers;

public sealed class RoomsController(EscapeHubApiClient api) : Controller
{
    private static readonly IReadOnlyDictionary<string, RoomGalleryImage[]> RoomGalleries =
        new Dictionary<string, RoomGalleryImage[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Kazamata"] =
            [
                new("A kazamata folyosója", "Sötét, kőfalú folyosó a kazamatában", "/images/rooms/dungeon_corridor.jpg"),
                new("A csapdakamra", "Mechanikus csapdákkal teli régi kőkamra", "/images/rooms/dungeon_trap.jpg"),
                new("Az elveszett kincsek", "Kincsesládák egy rejtett kazamatakamrában", "/images/rooms/dungeon_treasure.jpg")
            ],
            ["Gyilkossági rejtély"] =
            [
                new("A gyilkosság helyszíne", "Nyomokat rejtő, komor szoba", "/images/rooms/murder_scene.jpg"),
                new("A bizonyítékok", "Nyomozati bizonyítékok és fényképek", "/images/rooms/murder_evidence.jpg"),
                new("A lezárt ajtó", "Sötét folyosó egy zárt ajtóval", "/images/rooms/murder_door.jpg")
            ],
            ["Idegen invázió"] =
            [
                new("Az elhagyott űrhajó", "Egy elhagyatott űrhajó belső tere", "/images/rooms/alien_ship.jpg"),
                new("A vezérlőterem", "Világító konzolok az űrhajó vezérlőtermében", "/images/rooms/alien_control.jpg"),
                new("A legénység nyomában", "Az eltűnt legénység keresése közben feltárt helyszín", "/images/rooms/alien_search.jpg")
            ],
            ["Kastély"] =
            [
                new("A kastély főcsarnoka", "A kastély tágas, díszes főcsarnoka", "/images/rooms/mansion_hall.jpg"),
                new("A kastély lépcsőháza", "A kastély díszes lépcsőháza", "/images/rooms/mansion_stairs.jpg"),
                new("A régi könyvtár", "A kastély antik könyvtára", "/images/rooms/mansion_library.jpg")
            ],
            ["default"] =
            [
                new("A szoba hangulata", "Tematikus szabadulószoba illusztrációja", SvgId: "escapehub-room"),
                new("A kihívás részletei", "Rejtvényeket idéző szabadulószoba illusztrációja", SvgId: "escapehub-puzzle"),
                new("Kalandra fel", "Kalandos szabadulószoba illusztrációja", SvgId: "escapehub-adventure")
            ]
        };

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

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var result = await api.GetAsync<RoomDto>($"api/rooms/{id}");
        if (result.StatusCode == HttpStatusCode.NotFound) return NotFound();
        if (!result.Succeeded || result.Value is null)
        {
            TempData["Message"] = "A szoba adatainak betöltése nem sikerült. Ellenőrizze, hogy fut-e az API.";
            return RedirectToAction(nameof(Index));
        }

        var images = RoomGalleries.TryGetValue(result.Value.Name, out var roomImages)
            ? roomImages
            : RoomGalleries["default"];
        return View(new RoomPageModel(result.Value, images));
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(int slotId, int participantCount = 2)
    {
        var result = await api.PostAsync<object>("api/bookings", new BookingCreateRequest
        {
            TimeSlotId = slotId,
            ParticipantCount = participantCount
        });
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
