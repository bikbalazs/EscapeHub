# EscapeHub

Az EscapeHub egy ASP.NET Core MVC-vel, Entity Framework Core-ral és SQLite-tal készült szabadulószoba-foglaló weboldal. A forráskódban az azonosítók és a megjegyzések angolul szerepelnek; a látogatók által látott szövegek magyarul jelennek meg.

## Futtatás helyben

Telepítse a .NET 8 SDK-t. A weboldal minden adatot és műveletet az EscapeHub API-n keresztül ér el, ezért a `EscapeHub.Web` és az `EscapeHub.Api` projektet is futtatni kell.

Visual Studio-ban állítsa be az `EscapeHub.Web` és az `EscapeHub.Api` projektet több indítási projektként, és mindkettőnél a HTTPS-es `Project` profilt válassza.

Parancssorból indítsa el a két projektet külön PowerShell-ablakban, a tároló gyökérkönyvtárából:

```powershell
dotnet run --project EscapeHub.Api --launch-profile https
```

```powershell
dotnet run --project EscapeHub.Web --launch-profile https
```

Az alkalmazás első indításkor létrehozza az `escapehub.db` adatbázisfájlt a tároló gyökérkönyvtárában. Fejlesztői módban két bemutató-fiók érhető el:

| Szerepkör | E-mail-cím | Jelszó |
|---|---|---|
| Rendszergazda | `admin@escapehub.local` | `EscapeHubAdmin!2026` |
| Felhasználó | `user@escapehub.local` | `EscapeHubUser!2026` |

Ezek a nyilvános forráskódban szereplő, kizárólag helyi bemutatásra szánt fejlesztői fiókok. Ne telepítse az alkalmazást éles környezetben fejlesztői móddal, és ne használja ezeket a jelszavakat valódi adatokhoz. A fiókok csak akkor jönnek létre, ha az adott e-mail-cím még nem szerepel az adatbázisban.

Fejlesztői módban az első indításkor a rendszer létrehozza a négy magyar mintaszobát és a mai nap, valamint a következő két nap időpontjait. Az időpontok a budapesti helyi dátumhoz igazodnak: naponta 16:00-kor kezdődnek, és legkésőbb 22:00-kor érnek véget. A játékidőhöz 30 perc előkészítési idő adódik; a mintaszobák férőhelye 6 fő. Ha az alkalmazás 16:00 után indul el, az aznapi korábbi kezdések kimaradnak. A már létező szobákat és időpontokat az indítás nem írja felül.

Egyéni rendszergazdai fiók beállításához használjon környezeti változókat vagy .NET User Secrets szolgáltatást:

```powershell
$env:EscapeHub__AdminEmail = "admin@example.com"
$env:EscapeHub__AdminPassword = "egy-hosszu-egyedi-jelszo"
```

Az API-t az egyik terminálban, a weboldalt pedig a környezeti változókat beállító másik terminálban indítsa el:

```powershell
dotnet run --project EscapeHub.Api --launch-profile https
```

```powershell
dotnet run --project EscapeHub.Web --launch-profile https
```

A felhasználók az oldalon is regisztrálhatnak. A rendszergazdai felület csak rendszergazdai jogosultságú fiókkal érhető el.

## Az weboldal funkciói

- Regisztráció, bejelentkezés és kijelentkezés.
- Aktív szobák és jövőbeli időpontok böngészése.
- Szabad, szobához rendelt időpont lefoglalása és a foglalás lemondása.
- A rendszergazda szobákat vehet fel és szerkeszthet, időpontokat hirdethet meg és módosíthat, valamint felhasználókat hozhat létre és rendszergazdai jogosultságot adhat.
- A szobák játékideje percben állítható; az időpont befejezése automatikusan a játékidő és további 30 perc előkészítési idő utánra kerül.
- Aktív foglalással rendelkező időpont nem helyezhető át és nem inaktiválható. Az inaktivált szobák és időpontok az adatbázisban maradnak, így a foglalási előzmények megőrződnek.
- A rendszergazdák az időpontokat UTC szerint adják meg; az oldal magyarországi helyi idő szerint jeleníti meg őket (Europe/Budapest).

Az EscapeHub API kezeli az adatbázist, a fiókokat, a szobákat, az időpontokat és a foglalásokat. A Web alkalmazás szerveroldali HTTP-kérésekkel kommunikál az API-val; a két alkalmazás közös bejelentkezési kulcsokat használ.

Az SQLite-adatbázis helyi fejlesztéshez és prototípushoz készült. Törlés előtt készítsen róla biztonsági másolatot; a jelenlegi beállítás `EnsureCreated`-et használ adatbázis-migrációk helyett.

## Adatbázis-export

A [docs/adatbazis-export.sql](docs/adatbazis-export.sql) fájl az SQLite-adatbázis aktuális sémáját és korlátozásait tartalmazza, személyes vagy felhasználó által létrehozott rekordok nélkül. Fejlesztői módban a bemutató-fiókokat az API indításkor hozza létre.
