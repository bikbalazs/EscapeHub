using System.ComponentModel.DataAnnotations;
using EscapeHub.Core.Validation;

namespace EscapeHub.Core.DTOs;

public sealed record TimeSlotDto(
    int Id,
    int RoomId,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    bool IsActive,
    bool HasActiveBooking);

public sealed record RoomDto(
    int Id,
    string Name,
    string Description,
    int Capacity,
    int SolveDurationMinutes,
    bool IsActive,
    IReadOnlyList<TimeSlotDto> TimeSlots);

public sealed record BookingDto(
    int Id,
    int TimeSlotId,
    string RoomName,
    string CustomerEmail,
    int ParticipantCount,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    DateTime? CancelledAtUtc);

public sealed record UserDto(Guid Id, string Email, bool IsAdmin);

public sealed record AdminOverviewDto(
    IReadOnlyList<RoomDto> Rooms,
    IReadOnlyList<UserDto> Users);

public sealed class AccountRequest
{
    [Required(ErrorMessage = "Az e-mail-cím megadása kötelező.")]
    [EmailAddress(ErrorMessage = "Adjon meg érvényes e-mail-címet.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "A jelszó megadása kötelező.")]
    [MinLength(8, ErrorMessage = "A jelszó legalább 8 karakter legyen.")]
    public string Password { get; set; } = "";
}

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Az e-mail-cím megadása kötelező.")]
    [EmailAddress(ErrorMessage = "Adjon meg érvényes e-mail-címet.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "A jelszó megadása kötelező.")]
    public string Password { get; set; } = "";
}

public sealed class RoomSaveRequest
{
    [Required(ErrorMessage = "A szoba neve kötelező.")]
    [StringLength(120, ErrorMessage = "A szoba neve legfeljebb 120 karakter lehet.")]
    public string Name { get; set; } = "";

    [StringLength(2000, ErrorMessage = "A leírás legfeljebb 2000 karakter lehet.")]
    public string Description { get; set; } = "";

    [Range(2, 50, ErrorMessage = "A férőhely 2 és 50 közötti legyen.")]
    public int Capacity { get; set; }

    [AllowedSolveDuration(ErrorMessage = "Válasszon 60, 90 vagy 120 perces játékidőt.")]
    public int SolveDurationMinutes { get; set; } = 60;
}

public sealed class TimeSlotSaveRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Válasszon szobát.")]
    public int RoomId { get; set; }

    public DateTime StartsAtUtc { get; set; }
}

public sealed class BookingCreateRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Válasszon időpontot.")]
    public int TimeSlotId { get; set; }

    [Range(2, 50, ErrorMessage = "A résztvevők száma legalább 2 legyen.")]
    public int ParticipantCount { get; set; } = 2;
}

public sealed class AdminUserCreateRequest
{
    [Required(ErrorMessage = "Az e-mail-cím megadása kötelező.")]
    [EmailAddress(ErrorMessage = "Adjon meg érvényes e-mail-címet.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "A jelszó megadása kötelező.")]
    [MinLength(8, ErrorMessage = "A jelszó legalább 8 karakter legyen.")]
    public string Password { get; set; } = "";

    public bool IsAdmin { get; set; }
}

public sealed class SetAdminRequest
{
    public bool IsAdmin { get; set; }
}
