using EscapeHub.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace EscapeHub.Infrastructure.Data;

public sealed class EscapeHubDbContext(DbContextOptions<EscapeHubDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasKey(x => x.Id);
        modelBuilder.Entity<User>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<User>().Property(x => x.Email).HasMaxLength(320).IsRequired();
        modelBuilder.Entity<User>().Property(x => x.PasswordHash).IsRequired();

        modelBuilder.Entity<Room>().Property(x => x.Name).HasMaxLength(120).IsRequired();
        modelBuilder.Entity<Room>().Property(x => x.Description).HasMaxLength(2000);
        modelBuilder.Entity<Room>().Property(x => x.SolveDurationMinutes).HasDefaultValue(60);
        modelBuilder.Entity<Room>().HasMany(x => x.TimeSlots).WithOne(x => x.Room)
            .HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<TimeSlot>().HasIndex(x => new { x.RoomId, x.StartsAtUtc }).IsUnique();
        modelBuilder.Entity<TimeSlot>().HasMany(x => x.Bookings).WithOne(x => x.TimeSlot)
            .HasForeignKey(x => x.TimeSlotId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Booking>().HasIndex(x => x.TimeSlotId).IsUnique()
            .HasFilter("\"CancelledAtUtc\" IS NULL");
        modelBuilder.Entity<Booking>().HasOne(x => x.User).WithMany(x => x.Bookings)
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
