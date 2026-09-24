namespace EscapeHub.Core.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsAdmin { get; set; }
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
