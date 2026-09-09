# CLAUDE.md

Ez a fájl a repó **szerződése**: mi a kötelező minta, mi tilos, és hol van a részletes anyag.
Nem dokumentáció — ha valami itt szerepel, azt betartjuk; ha egy review-n drift derül ki, **először ez
a fájl (vagy a megfelelő skill) javul, és csak utána a kód**.

## Mi ez

Belső ebédrendelő alkalmazás. A dolgozók egy admin által megnyitott időszakra előre rendelnek A/B/C
menüből, aznap à la carte tételeket vesznek; az admin menüt szerkeszt, napokat zár ki, számláz; a
konyha összesítőt kap és napot zár.

Az app **működő, felépített rendszer, nem sablon**: EF Core + SQL Server LocalDB adatréteg
migrációkkal, MediatR CQRS use case-ek `AuthorizationBehavior` + `ValidationBehavior` pipeline-nal,
domain entitások, tesztekkel lefedett handlerek és komponensek.

> Ez a fájl **szándékosan nem tart nyilván darabszámot** (hány entitás, use case, teszt, migráció) —
> az minden committal elavul, az elavult szerződés pedig rosszabb, mint a semmi. Az aktuális állapotot
> a kódból nézd.

## Parancsok

```bash
dotnet build                                             # teljes solution
dotnet run --project EbedrendeloApp                      # app indítása
dotnet test                                              # összes teszt
dotnet test --filter "FullyQualifiedName~PlacePeriodOrder"   # egy use case tesztjei
```

A `dotnet` parancsok a repó gyökeréből futnak, a `.slnx` alapján.

## Solution felépítés

Lapos elrendezés, két projekt, `.slnx` solution formátum:

| Projekt | SDK | Szerep |
|---|---|---|
| `EbedrendeloApp/` | `Microsoft.NET.Sdk.Web` | app + domain + adathozzáférés + use case-ek (egy folyamat) |
| `EbedrendeloApp.Tests/` | `Microsoft.NET.Sdk.Razor` | tesztek, `ProjectReference`-szel az appra |

Az app mappaszerkezete: `Domain/` (Entities, Enums) · `Data/` (DbContext, Configurations, Migrations,
Seed) · `Features/<Terület>/<UseCase>/` · `Common/` (keresztmetsző: Results, Behaviors, Security, Time,
Calendar, Services, Formatting — és terület-specifikus közös logika: ALaCarte, Allergens, Billing,
Orders) · `Extensions/` (DI) · `Theme/` (MudBlazor téma) · `Components/` (Blazor UI).

A migrációk helye a `Data/Migrations/`, nem a projekt gyökerében lévő `Migrations/`.

## Architektúra — nem tárgyalható döntések

- .NET 10, **Blazor Web App**, **globális InteractiveServer** render mode. Ez nem változik — ne
  javasolj WebAssembly-t vagy static SSR-t.
- A globális interaktivitás az `App.razor`-ban van: a `Routes` és a `HeadOutlet` ugyanazt a
  `InteractiveServerRenderMode(prerender: false)` példányt kapja. **Emiatt egyetlen oldal se írjon saját
  `@rendermode` direktívát** — egy render mode határon belüli újabb `@rendermode` futásidejű kivételt dob.
- A **`prerender: false` szándékos döntés**, nem elnézés: prerenderrel minden `OnInitializedAsync`
  (és a benne futó lekérdezés) kétszer futna — egyszer a HTTP-válaszhoz, majd újra a SignalR-circuit
  csatlakozásakor —, miközben a prerenderelt oldal interaktivitás híján úgysem használható.
- Routing: `Components/Routes.razor`, a `Pages.NotFound` komponens explicit be van kötve
  (`NotFoundPage="typeof(Pages.NotFound)"`).
- Layout: `Components/Layout/MainLayout.razor` — MudBlazor `MudLayout`/`MudAppBar`/`MudDrawer`/
  `MudMainContent`, a négy MudBlazor providerrel és az `AppErrorBoundary`-vel. A `ReconnectModal` a
  SignalR-újracsatlakozást kezeli, maradjon.
- Stílus: **MudBlazor**. A Bootstrap ki lett vezetve (a `wwwroot/lib/bootstrap/` fájlok fizikailag még
  ott vannak, semmi nem hivatkozik rájuk). Komponensenkénti `.razor.css` (CSS isolation) használható.

## Kötelező minta: egy use case = egy mappa

`Features/<Terület>/<UseCase>/`, benne:

| Fájl | Tartalom |
|---|---|
| `<UseCase>Command.cs` / `Query.cs` | `sealed record` request `IRequest<…>`-szel, mellette a válasz-recordok |
| `<UseCase>Handler.cs` | `sealed class`, primary constructor, `IRequestHandler<,>` |
| `<UseCase>Validator.cs` | `sealed class : AbstractValidator<TRequest>` — a formai ellenőrzés ide való, nem a handlerbe |

Referencia-példa: `Features/Orders/PlacePeriodOrder/`. A teszt tükrözi a szerkezetet: a norma a
**lapos elrendezés** a terület alatt (`EbedrendeloApp.Tests/Features/Orders/PlacePeriodOrderHandlerTests.cs`),
nem use case-enkénti almappa.

- **`Result` ott kötelező, ahol a use case üzleti hibát tud jelezni.** Minden parancs
  `IRequest<Result>` vagy `IRequest<Result<T>>`; a query akkor, ha van `Result.Failure` ága
  (pl. `GetOrderableDaysQuery`, `ResolveColleagueQuery`). Tisztán olvasó, hibázni képtelen query
  visszaadhat csupasz DTO-t (`IRequest<IReadOnlyList<OrderingPeriodDto>>`) — a `Result` burok ott csak
  ceremónia, ami a hívót fölösleges `.Value` kicsomagolásra kényszeríti.
- Hibát `Result.Failure(ErrorCodes.X, "magyar üzenet")` ad vissza, nem kivétel. A hibakód a
  `Common/Results/ErrorCodes.cs`-ből jön; új kódot oda kell felvenni.
- Jogosultság **jelölő interfésszel** dől el, a szerveren: `IRequireAdmin`, `IActsOnBehalfOf`
  (idegen `TargetUserId` csak adminnak), `IAuditedOnBehalfOf` (bárki bárkinek, de auditálva) —
  `Common/Security/AuthorizationMarkers.cs`. Új use case-t **jelölni kell**; a
  `UseCaseAuthorizationCoverageTests` ezt számon kéri.
- Dátum/idő: `IAppClock` (`Common/Time/`), soha nem `DateTime.Now`. Munkanap-számítás:
  `IWorkingDayCalculator` (`Common/Calendar/`).

Részletes, kódszintű végigvezetés: `ebedrendelo-usecase` skill.

## Adathozzáférés — kötelező

- **EF Core az egyetlen adathozzáférési technológia.** Nincs második ORM, nincs nyers ADO.NET.
- A DbContextet **a handler nyitja**, `IDbContextFactory`-n keresztül:
  `await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);` (kivétel: a
  `Program.cs` indulási migráció/seed).
- A megnyitott `EbedrendeloDbContext` **paraméterként átadható közös helpernek** — a tulajdonos így is
  a handler marad, a helper nem nyit sajátot. Ez a bevett minta a `Common/Services/` (`CreditService`,
  `NotificationService`, `MenuReassignmentService`, `KitchenClosureQueries`, `MenuDishAllergenLookup`)
  és a `Common/ALaCarte/ALaCarteOrderingGate` alatt. Amit ez **nem** enged: `IDbContextFactory`-t vagy
  DbContextet injektáló szolgáltatás, amit komponens is használ.
- **`.razor` komponens `IMediator`-t injektál**, soha nem DbContextet és nem repositoryt.
- **Entitás nem hagyhatja el a handlert.** Kifelé DTO megy, kézi `.Select(...)` projekcióval, már az
  EF lekérdezésben. A DTO-k helye: `Features/<Terület>/<Terület>Dtos.cs`, vagy a use case mellett, ha
  csak ott kell.
- Nincs repository-réteg és nincs mapper könyvtár (AutoMapper/Mapster) — a handler + a kézi projekció
  ezt a szerepet betölti.

## C# standardok

- File-scoped namespace. `sealed` alapértelmezésben — kivétel, ha öröklés van rá (`Common/Results/Result.cs`,
  amiből a `Result<T>` származik). Nincs `region`.
- Primary constructor a handlereken és a szolgáltatásokon.
- `sealed record` a requestre és a DTO-ra; `sealed class` az entitásra és a handlerre.
- Entitás-property `required`, ahol az érték kötelező.
- `CancellationToken` végigfűzve minden async hívásig; `Async` utótag (a MediatR `Handle` kivétel).
- Nullable engedélyezve solution szinten — a `?` szándékos jelzés, ne nyomd el `!`-lel. **Egy kivétel:**
  EF navigációs property dereferenciája LINQ-kifejezésben (`l.ALaCarteOrder!.Date`), ahol a fordító nem
  látja az `Include`/szűrés garanciáját. A `Result.Value!` **nem** ilyen — ott a `TryGetValue` a helyes
  megoldás.
- XML-doc oda, ahol **üzleti szabály** magyarázata kell (miért, nem mit).
- **A kommentek és a felhasználónak szóló szövegek nyelve magyar.**

## Blazor komponens-szabályok

- **Don't:** üzleti logika `.razor` fájlban. **Do:** MediatR use case-ben; a komponens megjelenít és
  eseményt továbbít.
- `@code` blokk — **nincs `.razor.cs` code-behind** (ma nulla ilyen fájl van).
- Egy publikus komponens fájlonként; a szülő felé `EventCallback<T>`.
- `Components/Pages/<Terület>/` tükrözi a `Features/<Terület>/` nevét; a keresztmetsző elemek
  (`PageHeader`, `ColleaguePicker`, `ConfirmDialog`, `DateNavigatorBar`) a `Components/Shared/`-ben.
- Minden új komponenshez bUnit teszt.

UI-konvenciók (szín-, gomb-, dialógus-, form-szabályok): `mudblazor-ui-first` és
`ebedrendelo-extensions` skill.

## Tiltólista

- ❌ Dapper vagy bármely második ORM · ❌ repository-réteg · ❌ AutoMapper/Mapster
- ❌ DbContext `.razor`-ban vagy komponens-szolgáltatásban · ❌ entitás visszaadása a UI-nak
  - **Egyetlen nevesített kivétel:** az `ICurrentUser` (`Common/Security/StubCurrentUser.cs`) ma
    `IDbContextFactory`-t használ, és gyakorlatilag minden oldal injektálja. Ez az Epic 9 cookie-alapú
    bejelentkezéséig szóló átmenet — **új szolgáltatás nem hivatkozhat rá példaként**.
- ❌ WebAssembly / static SSR · ❌ oldal-szintű `@rendermode` · ❌ `.razor.cs` code-behind
- ❌ Central Package Management — a verziók a `.csproj`-okban maradnak
- ❌ üzleti érték C# konstansban (ár, határidő, szerepkör) — ezek `AppSetting`-ből jönnek
- ❌ kézi CSS ott, ahol van MudBlazor komponens/utility megoldás
- ❌ `region`, ❌ `DateTime.Now`, ❌ némán elnyelt kivétel

## Stack

| Csomag | Szerep |
|---|---|
| MudBlazor 9.8.0 | UI könyvtár — `AddMudServices()`, providerek a `MainLayout`-ban |
| MudBlazor.ThemeManager 4.0.0 | téma-szerkesztő — telepítve, nincs használatban |
| MediatR 14.2.0 | use case = request + handler; `AddMediatR` + `AuthorizationBehavior` és `ValidationBehavior` open behaviorök (`Extensions/ApplicationServiceCollectionExtensions.cs`) |
| FluentValidation 12.1.1 | validáció — `AddValidatorsFromCurrentAssembly`, a pipeline futtatja |
| GreatIdeas.Blazored.FluentValidation 3.0.0 | FluentValidation Blazor formokhoz (az eredeti `Blazored.FluentValidation` közösségi forkja — dokumentáció-kereséskor félrevihet) |
| Microsoft.EntityFrameworkCore.SqlServer / .Design 10.0.11 | adathozzáférés, SQL Server LocalDB, migrációk |

Tesztoldal: **bUnit 2.9.0 + xUnit v2** (`xunit 2.9.3`) — nem xUnit v3, hiába .NET 10; ez a párosítás
fordul és fut. A teszt SDK szándékosan `Microsoft.NET.Sdk.Razor`, hogy a tesztek `.razor`-ban is
írhatók legyenek. Sqlite in-memory a handler-tesztekhez (`TestSupport/SqliteDbContextFactory.cs`),
`FakeMediator` + `MudBunitContext` a komponens-tesztekhez.

**MudBlazor statikus assetek:** az `App.razor` a `@Assets[...]` helperen át hivatkozza a MudBlazor
CSS/JS-t, így megkapja a .NET 10 `MapStaticAssets` fingerprintjét. A MudBlazor saját dokumentációja
fingerprint nélküli útvonalat mutat; itt szándékosan az `@Assets` verzió van. A Roboto font a Google
Fontsról jön — ha a belső hálózat tiltja, a `<link>` kivehető, a MudBlazor rendszerfontra esik vissza.

## Tervdokumentumok

A `.claude/plans/` alatt, **egyetlen példányban** (máshol — home `.claude/plans/`, `docs/`, gyökér —
ne keletkezzen belőlük másolat):

| Fájl | Tartalom |
|---|---|
| `01-szerver-architektura.md` | adatmodell, üzleti szabályok algoritmusa, use case-ek, végrehajtási sorrend |
| `02-user-stories.md` | user story-k + elfogadási kritériumok + lefedettségi mátrix |
| `03-nyitott-teendok.md` | backlog: nem blokkoló észrevételek, driftek, ötletek |

Domain-feladat előtt ezekből indulj ki. Ha a use case készlet változik, a `01`-et és a `02`-t
**együtt** kell frissíteni; a kapocs a `02` végén lévő mátrix.

## Skillek és minőség-kapuk

| Terület | Skill |
|---|---|
| Use case írása (request/handler/validator/teszt) | `ebedrendelo-usecase` |
| Domain-konvenciók, megjelenítési helperek, form-minta | `ebedrendelo-extensions` |
| MudBlazor UI-szabályok | `mudblazor-ui-first` |
| C# minőség / konkurencia / DI | `csharp-coding-standards`, `csharp-concurrency-patterns`, `microsoft-extensions-dependency-injection` |
| EF Core, adat-teljesítmény | `efcore-patterns`, `database-performance` |
| Tesztelés | `dotnet-blazor-testing`, `snapshot-testing`, `playwright-blazor` |

.NET-es feladatnál a pretraining-emlékezet helyett a retrieval-alapú tudást részesítsd előnyben: nézd
át a repó meglévő mintáit, majd hívd meg a releváns skillt névvel, mielőtt implementálsz. A legkisebb
változtatással implementálj, és jelezd, ha a skill ajánlása ütközik a fenti döntésekkel.

Minőség-kapuk: `slopwatch` jelentős új/refaktorált kód után · `crap-analysis` összetett kódhoz tartozó
tesztek módosítása után · `/code-review` a diffre.

Nem relevánsak ehhez a projekthez (Aspire, Akka.NET, DocFX, MAUI, Uno) — csak akkor vonatkoztass
rájuk, ha a stack ténylegesen bővül ilyen irányba.

## Munkamódszer

- A válasz legyen tömör: felsorolás bekezdés helyett.
- Kutatásból vagy skillből származó állítást hivatkozz a forrásra (fájl:sor vagy skill neve).
- Kódváltoztatásnál: terv/diff, aztán végrehajtás — ne kérdezz rá minden lépésre.
- UI-komponensnél artifact-iteráció: generálás → te szerkeszted → visszajelzés.
- Tisztázó kérdést mindig tegyél fel, ha a feladat nem egyértelmű. Ha a feladatot nem tudod teljesíteni, jelezd, és
  indokold.
