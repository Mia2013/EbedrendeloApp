# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Mi ez

Belső ebédrendelő alkalmazás. A dolgozók napi menüt választanak, a konyha összesítőt kap.

**Fontos:** az app jelenleg még lényegében az érintetlen `dotnet new blazor` sablon. A `Counter`, `Weather` és `Home` oldalak sablon-maradványok, nem domain kód — nyugodtan törölhetők, amint az első valódi funkció elkészül. Domain modell, adathozzáférés és üzleti logika még nem létezik.

## Tervdokumentumok

A `.claude/plans/` alatt él a két mérvadó terv — **egyetlen példányban**, máshol (home `.claude/plans/`,
`docs/`, projekt gyökér) ne keletkezzen belőlük másolat:

| Fájl | Tartalom |
|---|---|
| `01-szerver-architektura.md` | adatmodell, üzleti szabályok, MediatR use case-ek, végrehajtási sorrend |
| `02-user-stories.md` | user story-k + elfogadási kritériumok + use case ↔ story lefedettségi mátrix |
| `03-nyitott-teendok.md` | backlog: fejlesztés közben felmerülő, nem blokkoló észrevételek/ötletek |

Domain-feladat előtt ezekből indulj ki, ne a sablonkódból. Ha a use case készlet változik, **mindkettőt**
frissíteni kell; a kapocs a `02` végén lévő lefedettségi mátrix.

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

Az app csomagjairól lásd a „Stack" szekciót — nem mindegyik van bekötve.

## Architektúra

- .NET 10, **Blazor Web App**, **globális InteractiveServer** render mode. Ez a döntés nem változik — ne javasolj WebAssembly-t vagy static SSR-t.
- A globális interaktivitás az `App.razor`-ban van beállítva: `<Routes @rendermode="InteractiveServer" />` és ugyanez a `HeadOutlet`-en. A MudBlazor drawer/dialógus/snackbar ezt megköveteli.
- **Emiatt egyetlen oldal se írjon saját `@rendermode` direktívát** — egy render mode határon belüli újabb `@rendermode` futásidejű kivételt dob. A `Counter.razor`-ból pont ezért lett eltávolítva.
- Routing: `Components/Routes.razor`, a `NotFoundPage` explicit be van kötve.
- Layout: `Components/Layout/MainLayout.razor` — MudBlazor `MudLayout` / `MudAppBar` / `MudDrawer` / `MudMainContent`, a négy MudBlazor providerrel a tetején. A `ReconnectModal` a Blazor Server SignalR-újracsatlakozást kezeli, maradjon.
- Stílus: **MudBlazor**. A Bootstrap ki lett vezetve az `App.razor`-ból; a `wwwroot/lib/bootstrap/` fájlok még fizikailag ott vannak, de semmi nem hivatkozik rájuk. Emellett komponensenkénti `.razor.css` (CSS isolation) használható.

## Szabályok

- **Don't:** üzleti logika `.razor` fájlban. **Do:** külön rétegben, a komponens csak megjelenít és eseményt továbbít.
- **Don't:** adathozzáférés közvetlenül komponensből. **Do:** DI-n keresztül injektált service/handler mögött.
- Minden új komponenshez bUnit teszt.

Ezek betartása teszi tesztelhetővé az appot: a logika-tesztek bUnit nélkül, ezredmásodperc alatt futnak, a bUnit csak a renderelést és az interakciót ellenőrzi.

## Teszt projekt sajátosságai

- **bUnit 2.9.0 + xUnit v2** (`xunit 2.9.3`) — nem xUnit v3, hiába .NET 10. Ez a párosítás fordul és fut, ellenőrizve.
- Az SDK szándékosan `Microsoft.NET.Sdk.Razor` és nem a sablon szerinti sima `Microsoft.NET.Sdk` — így a tesztek `.razor` fájlban is írhatók, nem csak C#-ban.
- A `UnitTest1.cs` üres placeholder, törölhető az első valódi teszttel.

## Stack

| Csomag | Szerep | Állapot |
|---|---|---|
| MudBlazor 9.8.0 | UI könyvtár | **Bekötve** — `AddMudServices()`, `@using MudBlazor` az `_Imports`-ban, CSS/JS az `App.razor`-ban, providerek a `MainLayout`-ban |
| MudBlazor.ThemeManager 4.0.0 | téma-szerkesztő | telepítve, nincs használatban |
| MediatR 14.2.0 | use case = request + handler | **csak telepítve** — nincs `AddMediatR(...)` regisztráció, és nincs egyetlen handler sem |
| FluentValidation 12.1.1 | validáció | **csak telepítve** — nincs validátor osztály, nincs DI regisztráció |
| GreatIdeas.Blazored.FluentValidation 3.0.0 | FluentValidation Blazor formokhoz | **csak telepítve** — nincs `<FluentValidationValidator />` sehol |

A „csak telepítve" sorokra ne írj kódot a bekötés elvégzése nélkül — a hiányzó DI regisztráció fordításkor nem derül ki, csak futásidőben.

Megjegyzés: a `GreatIdeas.Blazored.FluentValidation` az eredeti `Blazored.FluentValidation` közösségi forkja, nem maga az eredeti csomag — dokumentáció kereséskor ez félrevihet.

## MudBlazor statikus assetek

Az `App.razor` a `@Assets[...]` helperen keresztül hivatkozza a MudBlazor CSS/JS-t (`_content/MudBlazor/MudBlazor.min.css`, `.js`). Ez a .NET 10 `MapStaticAssets` fingerprintjét is megkapja — ellenőrizve, hogy a kiszolgált URL `MudBlazor.min.<hash>.css` alakú és 200-zal jön vissza. A MudBlazor saját dokumentációja fingerprint nélküli sima útvonalat mutat; itt szándékosan az `@Assets` verzió van, a projekt többi assetjével egységesen.

A Roboto font a Google Fontsról töltődik. Ha a belső hálózat ezt tiltja, a `<link>` kivehető — a MudBlazor rendszerfontra esik vissza.

## dotnet-skills plugin

A `dotnet-skills` Claude Code plugin telepítve van (`Aaronontheweb/dotnet-skills` marketplace). .NET-es feladatnál a pretraining-emlékezet helyett részesítsd előnyben a retrieval-alapú tudást: nézd át a repó meglévő mintáit, majd hívd meg a releváns skillt névvel (`Skill` tool), mielőtt implementálsz. Legkisebb változtatással implementálj, és jelezd, ha a skill ajánlása ütközik a fenti architektúra-döntésekkel (pl. render mode, MudBlazor).

Routing (a ténylegesen telepített skill-nevekkel — ha a plugin frissül, ellenőrizd újra):

| Terület | Skill |
|---|---|
| C# minőség / konkurencia | `csharp-coding-standards`, `csharp-concurrency-patterns`, `csharp-api-design`, `csharp-type-design-performance`, `csharp-nullable-reference-types` |
| DI / konfiguráció | `microsoft-extensions-dependency-injection`, `microsoft-extensions-configuration` |
| Tesztelés | `playwright-blazor` (bUnit-en felül, ha UI-teszt bővül), `snapshot-testing` |
| Adat (ha bekerül EF Core) | `efcore-patterns`, `database-performance` |

Minőség-kapuk:
- `slopwatch` — jelentős új/refaktorált/LLM-generált kód után
- `crap-analysis` — összetett kódhoz tartozó tesztek hozzáadása/módosítása után

Jelenleg nem relevánsak ehhez a projekthez (Aspire, Akka.NET, DocFX) — csak akkor vonatkoztass rájuk, ha a stack ténylegesen bővül ilyen irányba.

## Rules
- Keep reports concise (bullet points over paragraphs)
- Cite sources from research or skills
- For code changes: Show diff/plan briefly, then execute  
- For components: Use artifact iteration (generate → you edit → feedback)
- Ask clarifying questions only if task is truly ambiguous
