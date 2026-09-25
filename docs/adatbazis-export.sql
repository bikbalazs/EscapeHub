-- Az EscapeHub SQLite-adatbazisanak sémája és biztonságos bemutatóadatai.
-- A bemutató admin- és felhasználói fiók jelszava a README-ben található.
-- Személyes fiókokat és foglalásokat nem tartalmaz.
-- A futtatáskor dinamikusan létrehozott időpontok a gép helyi időzónáját használják;
-- a helyes budapesti időkhöz a rendszer időzónája legyen Europe/Budapest.
-- Üres adatbázison futtassa. A projekt önmagában is létrehozza a bemutatóadatokat.

PRAGMA foreign_keys = OFF;
BEGIN TRANSACTION;

CREATE TABLE "Users" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY,
    "Email" TEXT NOT NULL,
    "PasswordHash" TEXT NOT NULL,
    "IsAdmin" INTEGER NOT NULL
);

CREATE TABLE "Rooms" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Rooms" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "Capacity" INTEGER NOT NULL,
    "SolveDurationMinutes" INTEGER NOT NULL DEFAULT 60,
    "IsActive" INTEGER NOT NULL
);

CREATE TABLE "TimeSlots" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_TimeSlots" PRIMARY KEY AUTOINCREMENT,
    "RoomId" INTEGER NOT NULL,
    "StartsAtUtc" TEXT NOT NULL,
    "EndsAtUtc" TEXT NOT NULL,
    "IsActive" INTEGER NOT NULL,
    CONSTRAINT "FK_TimeSlots_Rooms_RoomId" FOREIGN KEY ("RoomId") REFERENCES "Rooms" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "Bookings" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Bookings" PRIMARY KEY AUTOINCREMENT,
    "TimeSlotId" INTEGER NOT NULL,
    "ParticipantCount" INTEGER NOT NULL,
    "UserId" TEXT NOT NULL,
    "CreatedAtUtc" TEXT NOT NULL,
    "CancelledAtUtc" TEXT NULL,
    CONSTRAINT "FK_Bookings_TimeSlots_TimeSlotId" FOREIGN KEY ("TimeSlotId") REFERENCES "TimeSlots" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Bookings_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_Bookings_TimeSlotId" ON "Bookings" ("TimeSlotId") WHERE "CancelledAtUtc" IS NULL;
CREATE INDEX "IX_Bookings_UserId" ON "Bookings" ("UserId");
CREATE UNIQUE INDEX "IX_TimeSlots_RoomId_StartsAtUtc" ON "TimeSlots" ("RoomId", "StartsAtUtc");
CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");

INSERT INTO "Users" ("Id", "Email", "PasswordHash", "IsAdmin") VALUES
('B8491956-B21B-4EAE-ACC3-EFCF00331627', 'admin@escapehub.local', 'AQAAAAIAAYagAAAAEIqwgeH80pdS0sjspbmz9T4rnhr6mM61LzL2OsVJItY6qXSxR3TxHyxT04XcGUuVFg==', 1),
('184C8EEA-8A8C-41F0-924B-F68AF480C4ED', 'user@escapehub.local', 'AQAAAAIAAYagAAAAEFsGcaFigvRUwOc/+wk1Q2vuWUPpH+6rxt1LQqxVuo4KJsUxcKTbD9E7qlKW/BqgOg==', 0);

INSERT INTO "Rooms" ("Id", "Name", "Description", "Capacity", "SolveDurationMinutes", "IsActive") VALUES
(1, 'Kazamata', 'A barátaiddal együtt egy rejtett szobákkal és csapdákkal teli kazamatába tévedtetek. Sikerül megszöknötök, mielőtt lejár az időtök? És a zsebeiteket is megtöltitek kinccsel, miközben a kijáratot keresitek? 60 percetek van a menekülésre.', 6, 60, 1),
(2, 'Gyilkossági rejtély', 'Nyomozóként téged és társaidat bíznak meg egy gyilkossági ügy felderítésével. A gyilkos azonban számított az érkezésetekre, és csapdába ejtett titeket. Jussatok ki a csapdából, göngyölítsétek fel az ügyet, és leplezzétek le a tettest — mindezt 90 perc alatt!', 6, 90, 1),
(3, 'Idegen invázió', 'Egy bajba jutott űrhajóról érkező vészjelzésre indultatok segítségül. Amikor megérkeztetek, a legénységnek nyoma veszett. Keressétek meg az eltűnt embereket, és derítsétek ki, miért adták le a segélyhívást. Sikerül mindezt 120 perc alatt?', 6, 120, 1),
(4, 'Kastély', 'Barátaiddal egy elhagyatott kastélyban találjátok magatokat. A kastély minden bejárata zárva van, és csak egy üzenetet találtok: „Ki e kastély falait elhagyni kívánja, a rejtély megfejtését lelje meg nyitjára.” Megfejtitek a kastély rejtvényeit, és sikerül kijutnotok 90 perc alatt?', 6, 90, 1);

WITH RECURSIVE
calendar(day_offset) AS (
    VALUES (0)
    UNION ALL SELECT day_offset + 1 FROM calendar WHERE day_offset < 2
),
starts(room_id, duration_minutes, day_offset, start_minute) AS (
    SELECT room."Id", room."SolveDurationMinutes", calendar.day_offset, 960
    FROM "Rooms" AS room CROSS JOIN calendar
    UNION ALL
    SELECT room_id, duration_minutes, day_offset, start_minute + duration_minutes + 30
    FROM starts
    WHERE start_minute + 2 * (duration_minutes + 30) <= 1320
),
utc_slots(room_id, starts_at_utc, ends_at_utc) AS (
    SELECT room_id,
        datetime('now', 'localtime', 'start of day', printf('+%d days', day_offset), printf('+%d minutes', start_minute), 'utc'),
        datetime('now', 'localtime', 'start of day', printf('+%d days', day_offset), printf('+%d minutes', start_minute + duration_minutes + 30), 'utc')
    FROM starts
)
INSERT INTO "TimeSlots" ("RoomId", "StartsAtUtc", "EndsAtUtc", "IsActive")
SELECT room_id, starts_at_utc, ends_at_utc, 1
FROM utc_slots
WHERE starts_at_utc > datetime('now');

COMMIT;
PRAGMA foreign_keys = ON;
