using EscapeHub.Core.DTOs;
using EscapeHub.Web.Models;
using EscapeHub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace EscapeHub.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class AdminController(EscapeHubApiClient api) : Controller
{
    public async Task<IActionResult> Index()
    {
        var result = await api.GetAsync<AdminOverviewDto>("api/admin/overview");
        if (!result.Succeeded) return ApiFailure(result, nameof(Index));
        return View(result.Value ?? new AdminOverviewDto([], []));
    }

    [HttpGet]
    public async Task<IActionResult> Bookings()
    {
        var result = await api.GetAsync<IReadOnlyList<BookingDto>>("api/admin/bookings");
        if (!result.Succeeded) return ApiFailure(result, nameof(Bookings));
        return View(result.Value ?? Array.Empty<BookingDto>());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelBooking(int id)
    {
        var result = await api.PostAsync<object>($"api/admin/bookings/{id}/cancel");
        TempData["Message"] = result.Succeeded ? "A foglalást lemondtuk." : result.Message ?? "Nem sikerült lemondani a foglalást.";
        return RedirectToAction(nameof(Bookings));
    }

    [HttpGet]
    public async Task<IActionResult> Room(int? id)
    {
        if (id is null) return View("Room", new RoomFormModel());
        var result = await api.GetAsync<RoomDto>($"api/admin/rooms/{id}");
        if (!result.Succeeded || result.Value is null) return result.StatusCode == HttpStatusCode.NotFound ? NotFound() : ApiFailure(result, nameof(Index));
        return View("Room", new RoomFormModel
        {
            Id = id,
            Name = result.Value.Name,
            Description = result.Value.Description,
            Capacity = result.Value.Capacity,
            SolveDurationMinutes = result.Value.SolveDurationMinutes
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRoom(RoomFormModel model)
    {
        if (!ModelState.IsValid) return View("Room", model);
        var body = new RoomSaveRequest
        {
            Name = model.Name.Trim(),
            Description = model.Description.Trim(),
            Capacity = model.Capacity,
            SolveDurationMinutes = model.SolveDurationMinutes
        };
        var result = model.Id is null
            ? await api.PostAsync<object>("api/admin/rooms", body)
            : await api.PutAsync<object>($"api/admin/rooms/{model.Id}", body);
        if (!result.Succeeded) { result.AddErrorsTo(ModelState); return View("Room", model); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateRoom(int id) => AdminAction(await api.PostAsync<object>($"api/admin/rooms/{id}/deactivate"));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ActivateRoom(int id) => AdminAction(await api.PostAsync<object>($"api/admin/rooms/{id}/activate"));

    [HttpGet]
    public async Task<IActionResult> Slot(int roomId, int? id)
    {
        var roomResult = await api.GetAsync<RoomDto>($"api/admin/rooms/{roomId}");
        if (!roomResult.Succeeded || roomResult.Value is null) return roomResult.StatusCode == HttpStatusCode.NotFound ? NotFound() : ApiFailure(roomResult, nameof(Index));
        ViewBag.RoomName = roomResult.Value.Name;
        ViewBag.SolveDurationMinutes = roomResult.Value.SolveDurationMinutes;
        var model = id is null
            ? new TimeSlotFormModel { RoomId = roomId, StartsAtUtc = DateTime.UtcNow.AddDays(1) }
            : await LoadSlot(roomId, id.Value);
        if (model is null) return NotFound();
        ViewBag.RoomId = roomId;
        return View("Slot", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSlot(TimeSlotFormModel model)
    {
        var roomResult = await api.GetAsync<RoomDto>($"api/admin/rooms/{model.RoomId}");
        if (!roomResult.Succeeded || roomResult.Value is null) return roomResult.StatusCode == HttpStatusCode.NotFound ? NotFound() : ApiFailure(roomResult, nameof(Index));
        ViewBag.RoomName = roomResult.Value.Name;
        ViewBag.SolveDurationMinutes = roomResult.Value.SolveDurationMinutes;
        ViewBag.RoomId = model.RoomId;
        model.StartsAtUtc = DateTime.SpecifyKind(model.StartsAtUtc, DateTimeKind.Utc);
        if (model.StartsAtUtc <= DateTime.UtcNow)
            ModelState.AddModelError(nameof(model.StartsAtUtc), "A kezdés legyen a jövőben.");
        if (!ModelState.IsValid) return View("Slot", model);
        var body = new TimeSlotSaveRequest { RoomId = model.RoomId, StartsAtUtc = model.StartsAtUtc };
        var result = model.Id is null
            ? await api.PostAsync<object>("api/admin/slots", body)
            : await api.PutAsync<object>($"api/admin/slots/{model.Id}", body);
        if (!result.Succeeded) { result.AddErrorsTo(ModelState); return View("Slot", model); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateSlot(int id) => AdminAction(await api.PostAsync<object>($"api/admin/slots/{id}/deactivate"));

    [HttpGet]
    public IActionResult Users() => View("User", new AdminUserModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveUser(AdminUserModel model)
    {
        if (!ModelState.IsValid) return View("User", model);
        var result = await api.PostAsync<object>("api/admin/users", new AdminUserCreateRequest
        {
            Email = model.Email.Trim(), Password = model.Password, IsAdmin = model.IsAdmin
        });
        if (!result.Succeeded) { result.AddErrorsTo(ModelState); return View("User", model); }
        TempData["Message"] = "A felhasználó létrejött.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAdmin(Guid id, bool isAdmin) =>
        AdminAction(await api.PostAsync<object>($"api/admin/users/{id}/role", new SetAdminRequest { IsAdmin = isAdmin }));

    private async Task<TimeSlotFormModel?> LoadSlot(int roomId, int id)
    {
        var result = await api.GetAsync<TimeSlotDto>($"api/admin/rooms/{roomId}/slots/{id}");
        return result.Succeeded && result.Value is not null
            ? new TimeSlotFormModel { Id = id, RoomId = roomId, StartsAtUtc = result.Value.StartsAtUtc }
            : null;
    }

    private IActionResult AdminAction(ApiCallResult<object> result)
    {
        if (!result.Succeeded) TempData["Message"] = result.Message ?? "A művelet nem sikerült.";
        return RedirectToAction(nameof(Index));
    }

    private IActionResult ApiFailure<T>(ApiCallResult<T> result, string returnAction)
    {
        TempData["Message"] = result.Message ?? "Az API nem érhető el. Ellenőrizze, hogy fut-e az API projekt.";
        return RedirectToAction(returnAction);
    }
}
