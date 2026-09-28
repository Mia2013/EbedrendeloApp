---
name: ebedrendelo-usecase
description: Egy MediatR use case anatómiája az EbedrendeloApp-ban — request/handler/validator/teszt négyes, Result és ErrorCodes, jogosultsági jelölők, IDbContextFactory-minta, DTO-projekció. Hívd meg új use case írása vagy meglévő módosítása előtt.
---

# Use case anatómia

Ez a skill a repóban **ténylegesen meglévő** mintát írja le. Minden kódrészlet valós fájlból való, a
forrás meg van nevezve — ha eltérést látsz a kód és az itt leírtak közt, a kód a mérvadó, és jelezd.

Kapcsolódó: általános szabályok a `CLAUDE.md`-ben, UI-oldal a `mudblazor-ui-first` és
`ebedrendelo-extensions` skillekben.

---

## 1. A négyes

Egy use case = egy mappa: `Features/<Terület>/<UseCase>/`

```
Features/Orders/PlacePeriodOrder/
  PlacePeriodOrderCommand.cs     request + válasz recordok
  PlacePeriodOrderHandler.cs     üzleti logika
  PlacePeriodOrderValidator.cs   formai ellenőrzés
```

és tükrözve a tesztprojektben:

```
EbedrendeloApp.Tests/Features/Orders/PlacePeriodOrderHandlerTests.cs
```

`Terület` a nyolc feature-terület egyike: `Admin`, `ALaCarte`, `Billing`, `Calendar`, `Kitchen`, `Menus`,
`Orders`, `Users`. Az `Admin` terület a több epicet átfogó admin nézeteké (pl. `GetAdminDashboardQuery`),
nem saját domainé. Új terület nyitása architektúra-döntés — előtte nézd meg a
`.claude/plans/01-szerver-architektura.md` 6. szakaszát.

---

## 2. Request — `sealed record`, jelölőkkel

`Features/Users/GetUsers/GetUsersQuery.cs`:

```csharp
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Users.GetUsers;

/// <summary>AC 9.4.1 — általános célú felhasználó-lista. …</summary>
public sealed record GetUsersQuery : IRequest<Result<IReadOnlyList<UserOptionDto>>>, IRequireAdmin;

public sealed record UserOptionDto(
    int Id,
    string UserName,
    int UserId,
    string DisplayName,
    string RoleName,
    string? Igazgatosag,
    string? Osztaly);
```

Amit ez a néhány sor rögzít:

- **`sealed record`**. A válasz típusa `Result<T>` (vagy `Result` érték nélkül) **ott, ahol a use case
  üzleti hibát tud jelezni** — minden parancsnál, és annál a query-nél, amelyiknek van `Result.Failure`
  ága (`GetOrderableDaysQuery`, `ResolveColleagueQuery`, `GetPeriodMenuQuery`, `GetMyPeriodOrderQuery`).
  Tisztán olvasó, hibázni képtelen query csupasz DTO-t ad vissza
  (`IRequest<IReadOnlyList<OrderingPeriodDto>>`): ott a `Result` burok üres ceremónia, ami a hívót
  fölösleges `.Value` kicsomagolásra kényszeríti. Ha bizonytalan vagy: **tud-e ez a use case olyan
  hibát adni, amit a felületnek meg kell mutatnia?** Ha nem, nincs `Result`.
- **XML-doc az AC-számmal**, ha a use case user story-ból jön — így a `02-user-stories.md`
  visszakereshető. Ide kerül az is, ha valamiért eltérünk az AC-től.
- **Jogosultsági jelölő a request-en**, nem a handlerben (lásd 4. pont).
- A DTO a request mellett él, ha csak ott kell; ha a terület több use case-e is használja, akkor
  `Features/<Terület>/<Terület>Dtos.cs`-ben (pl. `Features/Orders/OrderDtos.cs`).

Több paraméteres command, `IAuditedOnBehalfOf` jelöléssel
(`Features/Orders/PlacePeriodOrder/PlacePeriodOrderCommand.cs`):

```csharp
public sealed record PlacePeriodOrderCommand(
    int TargetUserId,
    int PlacedByUserId,
    int OrderingPeriodId,
    IReadOnlyList<DayOrderRequest> Days) : IRequest<Result<BatchOrderResult>>, IAuditedOnBehalfOf;

public sealed record DayOrderRequest(DateOnly Date, string VariantCode);
```

---

## 3. Handler — `sealed class`, primary ctor, `IDbContextFactory`

`Features/Users/GetUsers/GetUsersHandler.cs` — a legrövidebb teljes példa:

```csharp
public sealed class GetUsersHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetUsersQuery, Result<IReadOnlyList<UserOptionDto>>>
{
    public async Task<Result<IReadOnlyList<UserOptionDto>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var users = await db.Users.Include(u => u.Role)
            .OrderBy(u => u.VezetekNev).ThenBy(u => u.KeresztNev)
            .Select(u => new UserOptionDto(
                u.Id,
                u.UserName,
                u.UserId,
                (u.VezetekNev + " " + u.KeresztNev).Trim(),
                u.Role!.Name,
                u.Igazgatosag,
                u.Osztaly))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<UserOptionDto>>(users);
    }
}
```

Kötelező elemek:

| Elem | Miért |
|---|---|
| `await using var db = await dbFactory.CreateDbContextAsync(ct)` | Blazor Serverben a scoped DbContext élettartama a kör (circuit) — a factory adja a rövid életű, szálbiztos példányt |
| a megnyitott `db` **paraméterként** adható közös helpernek | a tulajdonos a handler marad; így működik a `CreditService`, `NotificationService`, `MenuReassignmentService`, `KitchenClosureQueries`, `MenuDishAllergenLookup` és az `ALaCarteOrderingGate`. Helper **nem** nyit saját contextet |
| `.Select(...)` **az EF lekérdezésben** | csak a szükséges oszlopok jönnek le, és entitás nem szivárog ki |
| `cancellationToken` minden async hívásba | a megszakadt kör ne dolgoztassa tovább az adatbázist |
| `Result.Success` / `Result.Failure` | a hiba üzleti kimenet, nem kivétel |

Több függőség (`Features/Orders/PlacePeriodOrder/PlacePeriodOrderHandler.cs`):

```csharp
public sealed class PlacePeriodOrderHandler(
    IDbContextFactory<EbedrendeloDbContext> dbFactory,
    IAppClock clock,
    IWorkingDayCalculator workingDayCalculator)
    : IRequestHandler<PlacePeriodOrderCommand, Result<BatchOrderResult>>
```

- **`IAppClock`** (`Common/Time/`) — `UtcNow`, `LocalNow`, `Today`, `ToLocal`. Soha ne `DateTime.Now`:
  a tesztek `FixedAppClock`-kal futnak.
- **`IWorkingDayCalculator`** (`Common/Calendar/`) — munkanap-számítás a kizárt napokkal együtt.
- **`ICreditService`, `INotificationService`, `IMenuReassignmentService`** (`Common/Services/`) — a
  több use case-en átívelő szabályok (jóváírás-könyvelés, értesítés írása, menü-átvezetés). Ha
  jóváírást vagy értesítést írnál kézzel a handlerben, előbb nézd meg ezeket.

Több entitást érintő írásnál tranzakció:

```csharp
await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
```

---

## 4. Jogosultság — jelölő interfész, nem `if`

`Common/Security/AuthorizationMarkers.cs`, kikényszerítve az `AuthorizationBehavior<,>`
pipeline behaviorral (a `ValidationBehavior` **előtt** fut: előbb dőljön el, hogy kérheti-e
egyáltalán, és csak utána, hogy jól kérte-e).

| Jelölő | Jelentés |
|---|---|
| `IRequireAdmin` | csak adminisztrátornak |
| `IActsOnBehalfOf` (`int TargetUserId { get; }`) | konkrét felhasználó adatára; **idegen** `TargetUserId` csak adminnak |
| `IAuditedOnBehalfOf` | szándékosan bárki bárkinek, a védelmet az audit adja (`PlacedByUserId`) |

Ha a request mezőneve `UserId` és nem `TargetUserId`, a record explicit implementációval képezi le.

Sértés esetén `ForbiddenException` repül (nem `Result.Failure`) — ez nem üzleti kimenet, hanem hiba
vagy visszaélés; a `MainLayout` `ErrorBoundary`-ja kezeli.

**Minden új use case-t jelölni kell.** Ha egyik jelölő sem illik rá, az `IntentionallyUnrestricted`
listába kell felvenni — a `UseCaseAuthorizationCoverageTests` architektúra-teszt ezt számon kéri, és
piros lesz, ha csendben kihagyod.

---

## 5. Validator — formai ellenőrzés, nem üzleti szabály

`Features/Orders/PlacePeriodOrder/PlacePeriodOrderValidator.cs`:

```csharp
public sealed class PlacePeriodOrderValidator : AbstractValidator<PlacePeriodOrderCommand>
{
    public PlacePeriodOrderValidator()
    {
        RuleFor(x => x.Days).NotEmpty();
        RuleFor(x => x.Days)
            .Must(days => days.Select(d => d.Date).Distinct().Count() == days.Count)
            .WithMessage("Egy dátum csak egyszer szerepelhet a listában.")
            .When(x => x.Days.Count > 0);

        RuleForEach(x => x.Days).ChildRules(day =>
        {
            day.RuleFor(d => d.VariantCode).NotEmpty();
        });
    }
}
```

- A validátor **regisztrációt nem igényel**: az `AddValidatorsFromCurrentAssembly`
  (`Extensions/ApplicationServiceCollectionExtensions.cs`) reflexióval megtalálja, a
  `ValidationBehavior<,>` pedig lefuttatja a handler előtt.
- Ide a **formai** szabály való (kötelező mező, duplikátum, tartomány). Ami adatbázis-lekérdezést
  igényel (létezik-e az időszak, lejárt-e a határidő), az a handlerbe megy `Result.Failure`-rel.
- Az üzenet **magyar**, a felhasználónak szól.

---

## 6. Hibakezelés — `Result` + `ErrorCodes`

```csharp
return Result.Failure<BatchOrderResult>(ErrorCodes.NotFound, "Az időszak nem található.");
```

- A hibakód konstans a `Common/Results/ErrorCodes.cs`-ből (`DeadlinePassed`, `PeriodClosed`,
  `DayClosed`, `DayExcluded`, `NotWorkingDay`, `MenuNotPublished`, `OutsidePeriod`, `AlreadyOrdered`,
  `NoActiveOrder`, `InvalidVariantCode`, `Overlaps`, `NotFutureDate`, `HasOrders`, `NotFound`,
  `NoVariants`, …). **Új hiba → új konstans oda**, ne string literál a handlerben.
- Részleges siker: a batch use case-ek (több nap egy hívásban) nem az egészet buktatják, hanem
  naponkénti eredményt adnak vissza — lásd `BatchOrderResult`.
- `Result.ToFailure<T>()` a közös, érték nélküli kapuk (pl. `ALaCarteOrderingGate`) hibáját fűzi át
  értéket visszaadó handlerbe.

---

## 7. Teszt — kötelező, Sqlite-tal

`EbedrendeloApp.Tests/Features/Orders/PlacePeriodOrderHandlerTests.cs` mintája:

```csharp
public class PlacePeriodOrderHandlerTests : IDisposable
{
    // 2026-08-17 is a Monday (same reference date used by GetOrderableDaysHandlerTests).
    private static readonly DateOnly Mon = new(2026, 8, 17);

    private readonly SqliteDbContextFactory dbFactory = new();

    public void Dispose() => dbFactory.Dispose();

    private PlacePeriodOrderHandler CreateHandler(DateTime nowLocal)
        => new(dbFactory, new FixedAppClock(nowLocal), new WorkingDayCalculator());
}
```

- `TestSupport/SqliteDbContextFactory.cs` — in-memory Sqlite, valódi EF viselkedéssel; a handler
  ugyanazt az `IDbContextFactory<EbedrendeloDbContext>`-et kapja, mint élesben.
- `TestSupport/FixedAppClock.cs` — rögzített idő, hogy a határidő-logika determinisztikus legyen.
  **Fix, hétköznapra eső referencia-dátumot válassz**, és írd oda kommentben, melyik nap az.
- Elnevezés: `<OsztályNeve>Tests.cs`, a mappa a `Features/<Terület>/` szerkezetet követi.
- Komponens-teszthez: `MudBunitContext` + `FakeMediator` (`TestSupport/`), lásd
  `EbedrendeloApp.Tests/Components/`.

---

## 8. Ellenőrző lista új use case-hez

- [ ] `Features/<Terület>/<UseCase>/` mappa, három fájllal
- [ ] request `sealed record`, XML-doc az AC-számmal; `Result<T>` csak ha tud üzleti hibát adni
- [ ] jogosultsági jelölő rajta (vagy tudatos felvétel az `IntentionallyUnrestricted` listába)
- [ ] handler `sealed class`, primary ctor, `IDbContextFactory`, `cancellationToken` mindenhol
- [ ] kifelé DTO megy, `.Select` projekcióval — entitás nem
- [ ] hibák `ErrorCodes` konstanssal, magyar üzenettel
- [ ] validátor a formai szabályokra
- [ ] tükrözött handler-teszt Sqlite + `FixedAppClock` párossal
- [ ] ha UI is jár hozzá: a komponens `IMediator`-t injektál, nem DbContextet
- [ ] ha a use case készlet változott: `01-szerver-architektura.md` **és** `02-user-stories.md`
      frissítve (a kapocs a `02` végén lévő lefedettségi mátrix)
