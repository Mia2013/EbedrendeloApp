# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Mi ez

Belső ebédrendelő alkalmazás. A dolgozók napi menüt választanak, a konyha összesítőt kap.

**Fontos:** az app jelenleg még lényegében az érintetlen `dotnet new blazor` sablon. A `Counter`, `Weather` és `Home` oldalak sablon-maradványok, nem domain kód — nyugodtan törölhetők, amint az első valódi funkció elkészül. Domain modell, adathozzáférés és üzleti logika még nem létezik.

## Parancsok

```bash
dotnet build                                    # teljes solution
dotnet run --project EbedrendeloApp             # app indítása
dotnet test                                     # összes teszt
dotnet test --filter "FullyQualifiedName~Counter"   # egy teszt / teszt osztály
```

A `dotnet` parancsok a repó gyökeréből futnak, a `.slnx` alapján.

## Solution felépítés

Lapos elrendezés, két projekt, új `.slnx` solution formátum:

| Projekt | SDK | Szerep |
|---|---|---|
| `EbedrendeloApp/` | `Microsoft.NET.Sdk.Web` | maga a Blazor app |
| `EbedrendeloApp.Tests/` | `Microsoft.NET.Sdk.Razor` | tesztek, `ProjectReference`-szel az appra |

Az app csomagjairól lásd a „Stack" szekciót — több van telepítve, mint amennyi be van kötve.

## Architektúra

- .NET 10, **Blazor Web App**, globális **InteractiveServer** render mode (`Program.cs`). Ez a döntés nem változik — ne javasolj WebAssembly-t vagy static SSR-t.
- Routing: `Components/Routes.razor`, a `NotFoundPage` explicit be van kötve.
- Layout: `Components/Layout/MainLayout.razor`. A `ReconnectModal` a Blazor Server SignalR-újracsatlakozást kezeli — sablon-elem, de az InteractiveServer mód miatt maradjon.
- Stílus: jelenleg Bootstrap (`wwwroot/lib/bootstrap/`) + komponensenkénti `.razor.css` (CSS isolation).

## Szabályok

- **Don't:** üzleti logika `.razor` fájlban. **Do:** külön rétegben, a komponens csak megjelenít és eseményt továbbít.
- **Don't:** adathozzáférés közvetlenül komponensből. **Do:** DI-n keresztül injektált service/handler mögött.
- Minden új komponenshez bUnit teszt.

Ezek betartása teszi tesztelhetővé az appot: a logika-tesztek bUnit nélkül, ezredmásodperc alatt futnak, a bUnit csak a renderelést és az interakciót ellenőrzi.

## Teszt projekt sajátosságai

- **bUnit 2.9.0 + xUnit v2** (`xunit 2.9.3`) — nem xUnit v3, hiába .NET 10. Ez a párosítás fordul és fut, ellenőrizve.
- Az SDK szándékosan `Microsoft.NET.Sdk.Razor` és nem a sablon szerinti sima `Microsoft.NET.Sdk` — így a tesztek `.razor` fájlban is írhatók, nem csak C#-ban.
- A `UnitTest1.cs` üres placeholder, törölhető az első valódi teszttel.

## Stack — telepítve, de MÉG NINCS BEKÖTVE

Az app csproj-ában szerepelnek az alábbi csomagok, **de egyik sincs bekonfigurálva**. Használat előtt a bekötés hiányzó lépéseit is meg kell csinálni, különben futásidőben dől el:

| Csomag | Szerep | Mi hiányzik |
|---|---|---|
| MudBlazor 9.8.0 (+ ThemeManager 4.0.0) | UI könyvtár | `AddMudServices()`, `@using MudBlazor`, a MudBlazor CSS/JS az `App.razor`-ban, és a provider komponensek (`MudThemeProvider`, `MudPopoverProvider`, `MudDialogProvider`, `MudSnackbarProvider`) a `MainLayout`-ban |
| MediatR 14.2.0 | use case = request + handler | `AddMediatR(...)` regisztráció, és még nincs egyetlen handler sem |
| FluentValidation 12.1.1 | validáció | validátor osztályok + DI regisztráció |
| GreatIdeas.Blazored.FluentValidation 3.0.0 | FluentValidation Blazor formokhoz | `<FluentValidationValidator />` a formokban |

Két dolgot érdemes fejben tartani:

- A `MainLayout` és az `App.razor` jelenleg **Bootstrap** markupot és stíluslapot tölt. A MudBlazorra váltás nem csak csomagtelepítés — a kettő párhuzamos futtatása stílusütközést okoz, szóval a váltáskor a Bootstrap hivatkozásokat ki kell venni.
- A `GreatIdeas.Blazored.FluentValidation` az eredeti `Blazored.FluentValidation` közösségi forkja, nem maga az eredeti csomag — dokumentáció kereséskor ez félrevihet.
