namespace EscapeHub.Core.Entities;

public sealed class TimeSlot
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room? Room { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
