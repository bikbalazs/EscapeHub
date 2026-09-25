using EscapeHub.Core.Entities;
using EscapeHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscapeHub.Api;

public static class DemoRoomSeeder
{
    private const int DefaultCapacity = 6;
    private const int SetupMinutes = 30;
    private const int DaysToSeed = 3;

    private sealed record RoomSeed(string Name, string Description, int SolveDurationMinutes);

    private static readonly RoomSeed[] Rooms =
    [
        new(
            "Kazamata",
            "A barátaiddal együtt egy rejtett szobákkal és csapdákkal teli kazamatába tévedtetek. Sikerül megszöknötök, mielőtt lejár az időtök? És a zsebeiteket is megtöltitek kinccsel, miközben a kijáratot keresitek? 60 percetek van a menekülésre.",
            60),
        new(
            "Gyilkossági rejtély",
            "Nyomozóként téged és társaidat bíznak meg egy gyilkossági ügy felderítésével. A gyilkos azonban számított az érkezésetekre, és csapdába ejtett titeket. Jussatok ki a csapdából, göngyölítsétek fel az ügyet, és leplezzétek le a tettest — mindezt 90 perc alatt!",
            90),
        new(
            "Idegen invázió",
            "Egy bajba jutott űrhajóról érkező vészjelzésre indultatok segítségül. Amikor megérkeztetek, a legénységnek nyoma veszett. Keressétek meg az eltűnt embereket, és derítsétek ki, miért adták le a segélyhívást. Sikerül mindezt 120 perc alatt?",
            120),
        new(
            "Kastély",
            "Barátaiddal egy elhagyatott kastélyban találjátok magatokat. A kastély minden bejárata zárva van, és csak egy üzenetet találtok: „Ki e kastély falait elhagyni kívánja, a rejtély megfejtését lelje meg nyitjára.” Megfejtitek a kastély rejtvényeit, és sikerül kijutnotok 90 perc alatt?",
            90)
    ];

    public static async Task SeedAsync(
        EscapeHubDbContext db,
        DateTime? utcNow = null,
        TimeZoneInfo? timeZone = null)
    {
        timeZone ??= GetBudapestTimeZone();
        var currentUtc = DateTime.SpecifyKind(utcNow ?? DateTime.UtcNow, DateTimeKind.Utc);
        var existingNames = (await db.Rooms.Select(room => room.Name).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var addedRooms = new List<(Room Room, RoomSeed Seed)>();

        foreach (var seed in Rooms)
        {
            if (!existingNames.Add(seed.Name)) continue;

            var room = new Room
            {
                Name = seed.Name,
                Description = seed.Description,
                Capacity = DefaultCapacity,
                SolveDurationMinutes = seed.SolveDurationMinutes,
                IsActive = true
            };
            db.Rooms.Add(room);
            addedRooms.Add((room, seed));
        }

        if (addedRooms.Count == 0) return;

        await db.SaveChangesAsync();
        foreach (var (room, seed) in addedRooms)
            SeedTimeSlots(db, room, seed, currentUtc, timeZone);
        await db.SaveChangesAsync();
    }

    private static void SeedTimeSlots(
        EscapeHubDbContext db,
        Room room,
        RoomSeed seed,
        DateTime currentUtc,
        TimeZoneInfo timeZone)
    {
        var today = TimeZoneInfo.ConvertTimeFromUtc(currentUtc, timeZone).Date;
        var intervalMinutes = seed.SolveDurationMinutes + SetupMinutes;

        for (var dayOffset = 0; dayOffset < DaysToSeed; dayOffset++)
        {
            var date = today.AddDays(dayOffset);
            for (var startMinute = 16 * 60; startMinute + intervalMinutes <= 22 * 60; startMinute += intervalMinutes)
            {
                var localStart = DateTime.SpecifyKind(date.AddMinutes(startMinute), DateTimeKind.Unspecified);
                var localEnd = localStart.AddMinutes(intervalMinutes);
                var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone);
                var endUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone);
                if (startUtc <= currentUtc) continue;

                db.TimeSlots.Add(new TimeSlot
                {
                    RoomId = room.Id,
                    StartsAtUtc = startUtc,
                    EndsAtUtc = endUtc,
                    IsActive = true
                });
            }
        }
    }

    private static TimeZoneInfo GetBudapestTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Budapest");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
        }
    }
}
