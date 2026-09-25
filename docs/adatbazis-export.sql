-- Az EscapeHub SQLite-adatbazisanak sémadumpja.
-- Személyes felhasználói, szoba-, időpont- és foglalási adatokat nem tartalmaz.
-- Fejlesztői módban a bemutató felhasználókat az EscapeHub.Api indításkor hozza létre.

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

COMMIT;
PRAGMA foreign_keys = ON;
