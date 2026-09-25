# Tesztelési eredmények

**Dátum:** 2026. szeptember 25.  
**Környezet:** .NET 8, SQLite memóriabeli adatbázissal  
**Futtatási parancs:** `dotnet exec "C:\Program Files\dotnet\sdk\8.0.425\MSBuild.dll" EscapeHub.sln -restore -target:VSTest -p:MSBuildSDKsPath="C:\Program Files\dotnet\sdk\8.0.425\Sdks" -p:VSTestVerbosity=minimal -verbosity:minimal`

## Eredmény

| Tesztprojekt | Sikeres | Sikertelen | Kihagyott |
|---|---:|---:|---:|
| `EscapeHub.Api.Tests` | 33 | 0 | 0 |
| `EscapeHub.Infrastructure.Tests` | 2 | 0 | 0 |
| **Összesen** | **35** | **0** | **0** |

## Ellenőrzött működések

- A szoba játékidejéből és a 30 perces előkészítésből számított időpont-végződés 60, 90 és 120 perces játékidőnél.
- Csak a 60, 90 vagy 120 perces játékidő fogadható el.
- Az átfedő aktív időpont visszautasítása, beleértve az előkészítési időt is.
- Egymást közvetlenül követő, átfedés nélküli időpontok engedélyezése.
- Egy időpont szerkesztésekor az időpont nem ütközik saját magával.
- Az adatbázis megakadályozza ugyanannak az időpontnak a kétszeri aktív foglalását, de engedi az új foglalást egy korábbi foglalás lemondása után.
- Regisztrációkor az e-mail-cím normalizálása, a jelszó hashelése és a már létező e-mail-cím visszautasítása.
- Bejelentkezés helyes jelszóval, valamint elutasítás hibás jelszó vagy nem létező fiók esetén; kijelentkezési művelet meghívása.
- Foglalás létrehozása, illetve visszautasítása nem létező, inaktív, lejárt vagy már aktívan foglalt időpontra.
- Foglalás lemondása csak a foglalás tulajdonosának, saját és már lemondott foglalás kezelése.
- A saját foglalások listája nem tartalmazza más felhasználók foglalásait.
- A nyilvános szobalista elrejti az inaktív szobákat és időpontokat, valamint jelzi az aktív foglalásokat.
- Szoba inaktiválásának visszautasítása aktív foglalás esetén, valamint inaktiválása foglalás nélkül; időpont inaktiválásának visszautasítása aktív foglalás esetén.

## Korlátozások

A tesztek közvetlenül hívják az API-vezérlő műveleteit SQLite memóriabeli adatbázissal, illetve ellenőrzik az adatbázis-korlátozásokat. A regisztrációs és bejelentkezési próbák rögzítik, hogy a vezérlő milyen felhasználót adna át bejelentkezésre; nem tesztelik a valódi cookie-middleware-t. Nem automatizált böngészős végponttól végpontig tesztek, és az API hitelesítési/antiforgery szűrőit sem futtatják. Ezek és a felületek kézi ellenőrzése külön feladat.

A tesztfuttatás sikeres volt. A NuGet a `System.Net.Http` 4.3.0 (`GHSA-7jgj-8wvc-jh57`) és a `System.Text.RegularExpressions` 4.3.0 (`GHSA-cmhx-cq75-c4mj`) csomagokhoz ismert, magas súlyosságú sebezhetőségekre figyelmeztetett. Ezek a figyelmeztetések nem akadályozták a tesztek futását, de a csomagfüggőségeket később frissíteni kell.
