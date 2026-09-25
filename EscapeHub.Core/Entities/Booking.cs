namespace EscapeHub.Core.Entities;

public sealed class Booking
{
    public int Id { get; set; }
    public int TimeSlotId { get; set; }
    public TimeSlot? TimeSlot { get; set; }
    public int ParticipantCount { get; set; } = 1;
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
}
