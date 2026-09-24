using System.ComponentModel.DataAnnotations;

namespace EscapeHub.Web.Models;

public sealed class RegisterModel
{
    [Required(ErrorMessage = "Az e-mail-cím megadása kötelező.")]
    [EmailAddress(ErrorMessage = "Adjon meg érvényes e-mail-címet.")]
    [Display(Name = "E-mail-cím")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "A jelszó megadása kötelező.")]
    [MinLength(8, ErrorMessage = "A jelszó legalább 8 karakter legyen.")]
    [DataType(DataType.Password)]
    [Display(Name = "Jelszó")]
    public string Password { get; set; } = "";
}

public sealed class LoginModel
{
    [Required(ErrorMessage = "Az e-mail-cím megadása kötelező.")]
    [EmailAddress(ErrorMessage = "Adjon meg érvényes e-mail-címet.")]
    [Display(Name = "E-mail-cím")]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "A jelszó megadása kötelező.")]
    [DataType(DataType.Password)]
    [Display(Name = "Jelszó")]
    public string Password { get; set; } = "";
}

public sealed class RoomFormModel
{
    public int? Id { get; set; }
    [Required(ErrorMessage = "A szoba neve kötelező.")]
    [StringLength(120, ErrorMessage = "A szoba neve legfeljebb 120 karakter lehet.")]
    [Display(Name = "Szoba neve")]
    public string Name { get; set; } = "";
    [StringLength(2000, ErrorMessage = "A leírás legfeljebb 2000 karakter lehet.")]
    [Display(Name = "Leírás")]
    public string Description { get; set; } = "";
    [Range(1, 50, ErrorMessage = "A férőhely 1 és 50 közötti legyen.")]
    [Display(Name = "Férőhely")]
    public int Capacity { get; set; } = 4;
}

public sealed class TimeSlotFormModel
{
    public int? Id { get; set; }
    public int RoomId { get; set; }
    [Required, Display(Name = "Kezdés (UTC)")]
    public DateTime StartsAtUtc { get; set; }
    [Required, Display(Name = "Befejezés (UTC)")]
    public DateTime EndsAtUtc { get; set; }
}

public sealed class AdminUserModel
{
    [Required(ErrorMessage = "Az e-mail-cím megadása kötelező."), EmailAddress(ErrorMessage = "Adjon meg érvényes e-mail-címet."), Display(Name = "E-mail-cím")]
    public string Email { get; set; } = "";
    [Required(ErrorMessage = "A jelszó megadása kötelező."), MinLength(8, ErrorMessage = "A jelszó legalább 8 karakter legyen."), DataType(DataType.Password), Display(Name = "Jelszó")]
    public string Password { get; set; } = "";
    [Display(Name = "Adminisztrátor")]
    public bool IsAdmin { get; set; }
}
