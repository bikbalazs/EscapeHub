namespace EscapeHub.Core.Entities;

public sealed class Room
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Capacity { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<TimeSlot> TimeSlots { get; set; } = new List<TimeSlot>();
}
