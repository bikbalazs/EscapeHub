# EscapeHub

Az EscapeHub egy magyar nyelvű, ASP.NET Core MVC-vel, Entity Framework Core-ral és SQLite-tal készült szabadulószoba-foglaló weboldal. A forráskódban az azonosítók és a megjegyzések angolul szerepelnek; a látogatók által látott szövegek magyarul jelennek meg.

## Futtatás helyben

Telepítse a .NET 8 SDK-t, majd a tároló gyökérkönyvtárából futtassa:

```powershell
dotnet run --project EscapeHub.Web
```

Az alkalmazás első indításkor létrehozza az `escapehub.db` adatbázisfájlt a futtatási könyvtárban. Fejlesztői módban két bemutató-fiók érhető el:

| Szerepkör | E-mail-cím | Jelszó |
|---|---|---|
| Rendszergazda | `admin@escapehub.local` | `EscapeHubAdmin!2026` |
| Felhasználó | `user@escapehub.local` | `EscapeHubUser!2026` |

Ezek a nyilvános forráskódban szereplő, kizárólag helyi bemutatásra szánt fejlesztői fiókok. Ne telepítse az alkalmazást éles környezetben fejlesztői móddal, és ne használja ezeket a jelszavakat valódi adatokhoz. A fiókok csak akkor jönnek létre, ha az adott e-mail-cím még nem szerepel az adatbázisban.

Egyéni rendszergazdai fiók beállításához használjon környezeti változókat vagy .NET User Secrets szolgáltatást:

```powershell
$env:EscapeHub__AdminEmail = "admin@example.com"
$env:EscapeHub__AdminPassword = "egy-hosszu-egyedi-jelszo"
dotnet run --project EscapeHub.Web
```

A felhasználók az oldalon is regisztrálhatnak. A rendszergazdai felület csak rendszergazdai jogosultságú fiókkal érhető el.

## Az első verzió funkciói

- Regisztráció, bejelentkezés és kijelentkezés.
- Aktív szobák és jövőbeli időpontok böngészése.
- Szabad, szobához rendelt időpont lefoglalása és a foglalás lemondása.
- A rendszergazda szobákat vehet fel és szerkeszthet, időpontokat hirdethet meg és módosíthat, valamint felhasználókat hozhat létre és rendszergazdai jogosultságot adhat.
- Aktív foglalással rendelkező időpont nem helyezhető át és nem inaktiválható. Az inaktivált szobák és időpontok az adatbázisban maradnak, így a foglalási előzmények megőrződnek.
- A rendszergazdák az időpontokat UTC szerint adják meg; az oldal magyarországi helyi idő szerint jeleníti meg őket (Europe/Budapest).

Az SQLite-adatbázis helyi fejlesztéshez és prototípushoz készült. Törlés előtt készítsen róla biztonsági másolatot; a jelenlegi beállítás `EnsureCreated`-et használ adatbázis-migrációk helyett.

