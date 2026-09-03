# Ebédrendelő — szerver oldali architektúra és implementációs terv

> **Ez a dokumentum egyetlen mérvadó példánya.** Más helyen (home `.claude/plans/`, `docs/`) ne
> keletkezzen belőle másolat.

## Context

Az `EbedrendeloApp` jelenleg egy érintetlen `dotnet new blazor` sablon MudBlazor-ra átöltöztetve: nulla
domain kód, nincs EF Core, nincs DbContext, a `Program.cs` csak a Razor komponenseket és a MudBlazor
szolgáltatásokat regisztrálja. A cél egy belső ebédrendelő alkalmazás, ahol a dolgozók havonta előre
rendelnek A/B/C menüből, aznap a la carte tételeket vehetnek, az adminisztrátor menüt szerkeszt, napokat
zár ki és kézzel jelöli a befizetéseket.

Ez a terv **kizárólag a szerver struktúrát** építi fel (a felhasználó kifejezett kérése): domain modell,
adatbázis, use case-ek, üzleti szabályok, seed adat, tesztek. A felület a legvégén, minimális szinten
jön — a sablonoldalak (`Counter`, `Weather`, `Home`) csak akkor törlődnek.

### Jóváhagyott technikai döntések

| Döntés | Választás |
|---|---|
| Adattárolás | EF Core + **SQL Server LocalDB**, migrációkkal (a gépen elérhető: `MSSQLLocalDB`, v17) |
| Rétegzés | **MediatR CQRS** (request + handler), FluentValidation pipeline behaviorral |
| Rendelési időszak | **Nem naptári hónap**, hanem az admin által megnyitott `[StartDate, EndDate]` tartomány (pl. aug. 5. – szept. 5.). Átfedés tilos, rés megengedett |
| Rendelési ablak | **Kétfázisú**: az `OrderDeadline`-ig bármely időszaki napra; utána a 3 munkanapos szabály szerintiekre, akár egyszerre az összesre. Több nap egy hívásban, részleges sikerrel |
| Értesítés | **In-app értesítés tábla** |
| Más nevében rendelés | **Bárki bárki nevében**, de naplózzuk, ki adta le (`PlacedByUserId`) |
| Jóváírás | **Egyenleg-könyvelés (ledger)**, azonnal felhasználható egyenlegként, automatikus beszámítás |
| Jóváírás hatóköre | **Csak menürendelésre számítható be** — a menü és a la carte pénzügy nem keveredik |
| Lemondás időhorizontja | **Aznapi lemondás nincs** — sem menüre, sem a la carte-ra |
| Munkanap | hétvége + **admin által felvitt kizárt napok** (ünnep is így kerül be) |
| A la carte készlet | **tételenkénti napi keret**, felhasználónként tételenként 1 db |
| Konyhai összesítő | **élő riport + „nap lezárása"** snapshot |

---

## 1. Mappastruktúra

Egyetlen web projekt marad (`EbedrendeloApp/`) — nincs indok új projektre, a CLAUDE.md lapos elrendezést
rögzít, és a Blazor Server app és az üzleti logika ugyanabban a folyamatban fut. A rétegeket **mappák**
választják el, a `Features/` alatt vertikális szeletekkel.

```
EbedrendeloApp/
├─ Domain/
│  ├─ Entities/          User, Role, AppSetting, DailyMenu, MenuVariant, MenuOrder,
│  │                     ALaCarteItem, ALaCarteDailyOffer, ALaCarteOrder, ALaCarteOrderLine,
│  │                     OrderingPeriod, ExcludedDay, CreditEntry, PeriodInvoice,
│  │                     UserNotification, KitchenClosure, KitchenClosureLine
│  └─ Enums/             OrderStatus, ALaCarteCategory, CancellationReason,
│                        CreditEntryKind, NotificationType
├─ Data/
│  ├─ EbedrendeloDbContext.cs
│  ├─ Configurations/    IEntityTypeConfiguration<T> entitásonként
│  ├─ Seed/              DatabaseSeeder.cs, SeedCatalog.cs
│  └─ Migrations/        (generált)
├─ Features/
│  ├─ Users/             Queries
│  ├─ Calendar/          kizárt napok, rendelési időszakok
│  ├─ Menus/             napi menü CRUD, variáns törlés + átvezetés
│  ├─ Orders/            időszaki menürendelés, lemondás
│  ├─ ALaCarte/          törzsadat, napi keret, aznapi rendelés
│  ├─ Kitchen/           összesítő, nap lezárása
│  ├─ Billing/           időszaki számla, jóváírás ledger, egyenleg, fizetés-jelölés
│  └─ Notifications/     lista, olvasottnak jelölés
├─ Common/
│  ├─ Results/           Result, Result<T>, ErrorCodes
│  ├─ Behaviors/         ValidationBehavior<TRequest,TResponse>
│  ├─ Time/              IAppClock, AppClock
│  ├─ Calendar/          IWorkingDayCalculator, WorkingDayCalculator  (ChangeDeadline)
│  ├─ Security/          ICurrentUser, CurrentUser, DevAuthEndpoints
│  └─ Services/          ICreditService, INotificationService, IMenuReassignmentService
├─ Extensions/           ServiceCollection extension metódusok
└─ Components/           (meglévő Blazor UI — a végén bővül)
```

Feature-en belüli konvenció: `Features/Orders/PlacePeriodOrder/` mappa, benne
`PlacePeriodOrderCommand.cs` (request + response record), `PlacePeriodOrderHandler.cs`,
`PlacePeriodOrderValidator.cs`. Így egy use case egy helyen van, és a CLAUDE.md szabálya
(„üzleti logika ne `.razor`-ban") szerkezetileg is kikényszerül.

---

## 2. Adatmodell

Konvenciók: pénz `int` (Ft, nincs tört), naptári nap `DateOnly`, időpillanat `DateTime` UTC-ben
`...AtUtc` utótaggal. Minden entitás `Id` int identity PK.

### User (a property-lista kötött, a felhasználó adta meg)
`int Id` (PK) · `int UserId` (céges azonosító, **unique index**) · `string UserName` (unique index, 64) ·
`string? KeresztNev` (128) · `string? VezetekNev` (128) · `string? Igazgatosag` (128) · `string? Osztaly` (128) ·
`string? Rf` (32) · `string? SzervKod` (32) · `int RoleId` (FK → Role)

`Igazgatosag` és `Osztaly` a felhasználó szöveges igazgatóság/osztály hovatartozását tárolja (pl. „Gyártás"
/ „1. üzem") — a `SzervKod`-tól (rövid szervezeti kód, szemantikája ismeretlen, l. „Nyitott kérdések")
függetlenül, arra logika nem épül, csak megjelenítési/szűrési adat.

### Role
`int Id` (PK) · `string Name` (32, unique — `"Admin"` / `"User"`)

A szerepkör **adatbázis-tábla**, nem C# konstans: két sorral seedelve indításkor
(`DatabaseSeeder.AdminRoleName` / `.UserRoleName` csak a seed-adat literálja, nem üzleti szabály). A
`"Admin"` **authorization policy név** ettől független — az ASP.NET Core kódba ágyazott azonosító
(5. fejezet), nem admin által szerkesztendő adat.

### AppSetting
Egyetlen sor (`Id = 1`), az admin által futásidőben módosítható üzleti paraméterek: `int MenuPortionHuf`,
`int ChangeDeadlineWorkingDays`, `TimeOnly ChangeDeadlineLocalTime`, `TimeOnly ALaCarteOrderDeadlineLocalTime`,
`DateTime UpdatedAtUtc`, `int? UpdatedByUserId`.

**Miért tábla, nem C# konstans.** A menü ára, a 3 munkanapos lemondási szabály és a két napi határidő
(11:00 / 10:30) mind olyan érték, amit egy admin ésszerűen újratelepítés nélkül akarna módosítani —
ezeket tehát adatként kell modellezni, nem `Domain/Constants`-ba zárt `const`/`static readonly` mezőként.
Csak a ténylegesen kódszintű azonosítók (pl. az `"Admin"` policy név, az `Europe/Budapest` IANA
időzóna-id a leendő `IAppClock` implementációban) maradnak C# szinten, a felhasználásuk helyéhez közel —
nem egy közös konstans-osztályban, ami üzleti adat bedobására csábítana.

### Naptár
- **ExcludedDay** — `Date` (unique), `Reason` (200), `CreatedAtUtc`, `CreatedByUserId` (FK User)
- **OrderingPeriod** — `Name` (64, admin-adott címke, pl. „2026. augusztus"), `StartDate` (unique),
  `EndDate` (unique), `OrderDeadline` (helyi `DateTime`), `IsOpen`, `CreatedAtUtc`;
  index `(StartDate, EndDate)` a dátum → időszak feloldáshoz

**Az időszak nem naptári hónap.** A `[StartDate, EndDate]` tartomány az, amire a rendelés leadható —
tarthat augusztus 5-től szeptember 5-ig is. Az `OrderDeadline` az az időpont, ameddig a leadás nyitva
van. Invariánsok (`UpsertOrderingPeriodValidator` + handler):

1. `StartDate <= EndDate`
2. `OrderDeadline <= ChangeDeadline(StartDate)` — a bulk ablak nem tarthat tovább, mint ameddig az
   időszak **első napja** amúgy is módosítható (3.1). Enélkül keletkezne egy anomália: ha a határidő
   csak egy nappal előzné meg a `StartDate`-et, az első napra a bulk ablakban még *lehetne rendelni*,
   de már *nem lehetne lemondani*. Így a két fázis hézag és anomália nélkül csatlakozik
3. **nincs átfedés** meglévő időszakkal: `NOT (StartDate <= p.EndDate AND EndDate >= p.StartDate)`
4. a `StartDate` / `EndDate` / `OrderDeadline` csak addig módosítható, **amíg az időszakhoz nem tartozik
   egyetlen rendelés sem**. Ha már van, csak a `Name` és az `IsOpen` írható — egyetlen nap kivétele
   ettől kezdve az `ExcludeDayCommand` dolga (3.6), ami az aznapi rendeléseket lemondja és jóváírja

A 3. pont SQL Serverben nem fejezhető ki constraintként (nincs exclusion constraint), ezért a handler
ellenőrzi, **serializable tranzakcióban**, hogy két párhuzamos felvitel se csússzon át. A `UQ StartDate`
és `UQ EndDate` csak részleges védelem: az azonos határú duplikátumot fogja meg, az átfedést nem.

**Rés megengedett** — létezhet munkanap, amely egyetlen időszakba sem esik. Ilyen napra sem havi
menürendelés, sem a la carte nem adható le (utóbbi azért, mert nem lenne mely számlára könyvelni).
A véletlen rést a `GetUncoveredWorkdaysQuery` teszi láthatóvá.

### Menü
- **DailyMenu** — `Date` (unique), `IsPublished`, `Note?`, **`RemovedAtUtc?`**; navigáció: `Variants`
- **MenuVariant** — `DailyMenuId` (FK, cascade), `Code` (`"A"`/`"B"`/`"C"`, unique `DailyMenuId`+`Code`),
  `Name`, `Description?`, `SortOrder` (az „első elérhető variáns" szabályhoz), **`RemovedAtUtc?`**

A menü ára az `AppSetting.MenuPortionHuf` értékét követi (induláskor 1400 Ft-ra seedelve), de a
rendelésre **snapshotoljuk**, hogy egy későbbi áremelés ne írja át a múltat.

**`RemovedAtUtc` — miért nincs fizikai törlés.** A `MenuOrder.MenuVariantId` FK-ja (lásd lejjebb)
`DeleteBehavior.Restrict`, mert egy rendelés — akár lemondva is — soha nem veszítheti el, melyik
variánsra szólt (audit). Emiatt egy `DailyMenu`/`MenuVariant` sor SQL-szintű törlése elszállna, mihelyt
bármelyik hozzá tartozó rendelés (akár egy régen lemondott is) még hivatkozik rá — ez pont a
`DeleteMenuVariantCommand`/`DeleteDailyMenuCommand` fő esetében állna elő, hiszen ott jellemzően *van*
rendelés a variánson. A `DeleteMenuVariantCommand` és a `DeleteDailyMenuCommand` ezért **soft delete**:
`RemovedAtUtc` beállítása, a sor megmarad. Minden olvasó lekérdezés (`GetDailyMenuQuery`,
`GetPeriodMenuQuery`, `GetTodayMenuForUserQuery`, `GetOrderableDaysQuery`) `RemovedAtUtc == null`-ra
szűr, így a felhasználó szempontjából ez ténylegesen törlésként viselkedik. A `Date` és a
`(DailyMenuId, Code)` unique index a soft-deletelt sorokra is érvényes marad — ha egy admin később
ugyanarra a napra vagy ugyanazzal a kóddal ad meg menüt/variánst, az `UpsertDailyMenuCommand` a régi sort
**élesztí fel** (`RemovedAtUtc = null`, mezők felülírva), nem új sort szúr be. Ugyanez a minta, mint a
`RemoveExcludedDayHandler`-ben a `CancelledByExcludedDayId` nullázása egy másik Restrict FK miatt — az
audit-megőrzés elve itt a menütörlésre is kiterjed.

**`SortOrder` a felületen implicit.** A mező a domainben megmaradt (erre épül a 3.2 „első maradék
variáns" szabálya), de az admin felület nem kéri be — a `MenuVariant.Code` (`"A"`/`"B"`/`"C"`) magától
adja a sorrendet, egy külön szám csak zavaró duplikáció lenne. Az `UpsertDailyMenuCommand`-ot hívó
felület a `Code` szerinti ábécésorrendből számítja ki a `SortOrder` értéket, mielőtt elküldi a parancsot.

- **MenuDish** — `Kind` (`Leves` / `Foetel`), `Name` (unique `Kind`+`Name`), `Allergens?`, valamint 7
  tápérték-mező: `EnergyKcal?`, `FatGrams?`, `SaturatedFatGrams?`, `CarbohydrateGrams?`, `SugarGrams?`,
  `ProteinGrams?`, `SaltGrams?`. Egy emlékeztető katalógus: a korábban valaha felvitt levesek/főételek
  neve, allergénlistája és tápértéke, kód-hivatkozás nélkül. A `MenuVariant.Name`/`Description` **nem**
  erre FK-ol, de a napi menü szerkesztő dialógus (`EditDailyMenuDialog`) mégsem enged szabad szöveget: a
  leves/főétel mező `MudAutocomplete`-tel **kizárólag** a katalógusból választ, egy nem egyező név
  begépelése után a mező visszaáll. Egy teljesen új leves/főétel csak külön képernyőn
  (`AddMenuDishDialog` → `CreateMenuDishCommand`) vihető fel a katalógusba — ennek admin felületi
  bekötése nyitott teendő (`03-nyitott-teendok.md`). A meglévő katalógustétel adatai az
  `UpsertDailyMenuCommand` mentésekor is frissülnek: minden mentett leves/főétel névhez, ha a
  katalógusban már létezik ilyen nevű sor, felülírja az allergén és tápérték mezőket (`UpdateMenuDishCommand`
  ugyanezt teszi explicit, a katalógus-szerkesztő felől). Az allergén és tápérték mezők felülírása csak
  akkor történik, ha a mentéskor nem üresek — egy üresen hagyott mező tehát *nem* törli a korábban
  rögzített adatot, csak a kitöltött érték ír felül. Ez adja az admin felület autocomplete-jét
  (`GetMenuDishSuggestionsQuery`) és az allergének/tápértékek megjelenítését is: a
  `GetDailyMenuQuery`/`GetPeriodMenuQuery`/`GetTodayMenuForUserQuery` mind visszaadja variánsonként a
  hozzátartozó leves/főétel allergénjeit és tápértékét, névre illesztve a katalógusból.

  **`Allergens` formátuma.** Nem szabad szöveg: a felület egy rögzített, 14 elemű, számozott listából
  (`Common/Allergens/AllergenCatalog.cs` — az EU 1169/2011 rendelet Annex II szerinti 14 hivatalos
  allergén, ~10 helyett a teljes jogilag elismert lista) enged multiszelektet, és a kiválasztott számokat
  vesszővel elválasztva tárolja (pl. `"1,7,9"`). A szám maga a megjelenítendő adat része — a magyar
  éttermi/konyhai gyakorlat a „7 – Tej (laktóz)" alakot használja, nem a puszta nevet —, ezért a szám a
  perzisztált értékben is megmarad, nem csak UI-generált sorindex. A szerver oldal (`UpsertDailyMenuHandler`,
  a DTO-k) ezt a stringet opakán kezeli — a katalógus és a „szám–név" formázás kizárólag UI-rétegbeli.

### Rendelés
- **MenuOrder** — `UserId` (FK), `Date`, **`OrderingPeriodId`** (FK, kötelező), `MenuVariantId` (FK),
  `PriceHuf` (snapshot), `Status` (`Active` / `Cancelled`), `PlacedByUserId` (FK User — ki adta le),
  `PlacedAtUtc`, `CancelledAtUtc?`, `CancelledByUserId?`, **`CancellationReason?`**,
  **`CancelledByExcludedDayId?`** (FK ExcludedDay), `ReassignedFromVariantCode?`, `ReassignedAtUtc?`
  - **Szűrt unique index** `(UserId, Date)` `WHERE Status = 0` → „napi menüből minden dolgozó csak 1 db"
  - Index `(Date, Status)` a konyhai összesítőhöz
  - Index `(OrderingPeriodId, UserId)` a számlageneráláshoz

**`OrderingPeriodId`** — a rendelés leadásakor **rögzül**, melyik időszakhoz tartozik; a számlázás ezen
joinol, nem dátumtartományt pásztáz. Így ha az admin utólag tágítja az időszak határait, a már leadott
rendelések nem vándorolnak át másik számlára.

**`CancellationReason`** (`ByUser` / `DayExcluded` / `MenuDeleted` / `VariantRemoved`) — a
`CancelledByUserId` önmagában nem árulja el, *miért* lett lemondva a rendelés: az admin lemondhat más
nevében is, és a saját rendelését is lemondhatja. A kizárás visszavonásához (3.7), az értesítés
szövegéhez és a ledger-nézethez viszont pontosan ezt kell tudni.

**`CancelledByExcludedDayId`** — nem elég a dátum szerinti visszakeresés: ha ugyanazt a napot kizárják,
visszavonják, majd újra kizárják, a két kör lemondásai összekeverednének. A konkrét `ExcludedDay`-re
mutató FK teszi egyértelművé, melyik kizárás melyik rendeléseket érintette.

### A la carte
- **ALaCarteItem** — `Name`, `Category` (`Leves`/`Foetel`/`Koret`/`Desszert`/`Ontet`), `PriceHuf`,
  `IsActive`, `Allergens?`, valamint — a `MenuDish` mintáját követve (2. fejezet) — 7 tápérték-mező:
  `EnergyKcal?`, `FatGrams?`, `SaturatedFatGrams?`, `CarbohydrateGrams?`, `SugarGrams?`, `ProteinGrams?`,
  `SaltGrams?`. Ez a katalógus **nem osztozik** a `MenuDish`/`MenuDishKind` katalóguson — teljesen külön,
  saját törzsadat.
- **ALaCarteDailyOffer** — `Date` + `ALaCarteItemId` (unique együtt), `Capacity`, `OrderedCount`
  → ez a napi keret; a `OrderedCount` atomikus növelése adja a készletfoglalást — **Leves kategóriájú
  tételre ez a mechanizmus nem vonatkozik** (lásd lejjebb). Naponta **legfeljebb egy aktív Leves
  ajánlat** állhat fenn — alkalmazásszintű szabály, nem DB-constraint (6. fejezet, `SetDailyOfferCommand`).
- **ALaCarteOrder** — `UserId` (FK), `Date` (unique `UserId`+`Date`), **`OrderingPeriodId`** (FK,
  kötelező), `PlacedAtUtc`, `PlacedByUserId`, `TotalHuf`
- **ALaCarteOrderLine** — `ALaCarteOrderId` (FK), `ALaCarteDailyOfferId` (FK),
  unique `(ALaCarteOrderId, ALaCarteDailyOfferId)` → tételenként 1 db;
  `ItemNameSnapshot`, `CategorySnapshot`, `UnitPriceHuf`, **`IncludesSoup`** (bool, snapshot — igaz, ha
  ez a sor Főétel és a rendelés pillanatában volt aznapra aktív Leves-ajánlat; a felület ebből, nem élő
  állapotból dönti el a „(levessel)" jelzést, mert a Leves-ajánlat vagy a főétel ára utólag változhat —
  NFR-7)

**Leves — korlátlan és díjtalan alap, a főétel árába rejtve.** A leves önálló `ALaCarteItem` sor
(`Category = Leves`), saját, a főételtől függetlenül szerkeszthető `PriceHuf`-fal — ha csak a levest
akarják drágítani, azt külön lehet megtenni. A dolgozó felé viszont **soha nem jelenik meg önálló,
árazott levessorként, és közvetlenül sem rendelhető**: `PlaceALaCarteOrderCommand` elutasít minden
Leves-ajánlatra irányuló közvetlen rendelést, és `GetDailyOffersQuery`/`GetTodayMenuForUserQuery` a
Leves-ajánlatot ki sem veszi fel a kínálati listába. Amikor egy Főétel-tételre rendelés érkezik, a
rendelési sor `UnitPriceHuf`-ja a főétel `PriceHuf`-jának és **aznapi** Leves-ajánlat `PriceHuf`-jának
összege — egyetlen kombinált szám a főétel neve alatt. Ha aznapra nincs Leves-ajánlat, a kombinált ár a
puszta főétel ára (leves-rész 0 Ft, nem hibaeset). A leves **soha nem kap külön `ALaCarteOrderLine`-t és
soha nem korlátozott készlettel** — akárhány (különböző) főételt rendel valaki aznapra, mindegyik sor
önállóan tartalmazza a leves árát, mert a konyha szemszögéből minden rendelt főételhez egy tányér leves
készül (US-4.6 AC 4.6.3).

### Pénzügy
- **CreditEntry** (ledger, előjeles, append-only) — `UserId` (FK), `AmountHuf` (+keletkezés /
  −felhasználás), `Kind` (`CancellationCredit` / `CreditApplied` / `CreditRevoked` / `ManualAdjustment`),
  `CreatedAtUtc`, `CreatedByUserId`, `Note?`
  - `SourceMenuOrderId?` → **melyik lemondott rendelésből keletkezett** (dátum + variáns onnan olvasható)
  - `RemainingHuf` → a még fel nem használt rész (pozitív tételeken karbantartva). **Az egyenleg ezek
    összege**, és azonnal felhasználható — nincs várakozási idő.
  - `ConsumesCreditEntryId?` + `PeriodInvoiceId?` → negatív tételeken: **mikor és melyik számlából**
    vonódott le
  - Így a felhasználó ledger-nézete tételesen mutatja: *mit mondott le → mennyi jóváírás keletkezett →
    mennyi az egyenlege → melyik időszaki számla menürészéből, mikor vonódott le.*
  - **Minden jóváírás menü-hatókörű** (a `ManualAdjustment` is): a beszámítás kizárólag a számla
    menütételeit csökkentheti — lásd 3.3.
- **PeriodInvoice** — `UserId` (FK), **`OrderingPeriodId`** (FK; unique `UserId`+`OrderingPeriodId`),
  `MenuGrossHuf`, `ALaCarteGrossHuf`, `GrossHuf`, `CreditAppliedHuf`, **`MenuPayableHuf`**,
  **`ALaCartePayableHuf`**, `PayableHuf`, `IsPaid`, `PaidAtUtc?`, `MarkedPaidByUserId?`, `GeneratedAtUtc`
  - Invariáns: `CreditAppliedHuf <= MenuGrossHuf`;
    `MenuPayableHuf = MenuGrossHuf - CreditAppliedHuf`;
    `ALaCartePayableHuf = ALaCarteGrossHuf`;
    `PayableHuf = MenuPayableHuf + ALaCartePayableHuf`

### Egyéb
- **UserNotification** — `UserId` (FK), `Type`, `Title`, `Message`, `RelatedDate?`, `RelatedMenuOrderId?`,
  `CreatedAtUtc`, `ReadAtUtc?`; index `(UserId, ReadAtUtc, CreatedAtUtc)`
  - *Szándékosan `UserNotification` és nem `Notification` — a MediatR `INotification` típusával
    ütközne a névtér-feloldásban.*
- **KitchenClosure** — `Date` (unique), `ClosedAtUtc`, `ClosedByUserId`, `TotalPortions`
- **KitchenClosureLine** — `KitchenClosureId` (FK), `VariantCode`, `VariantNameSnapshot`, `Quantity`

---

## 3. Üzleti szabályok algoritmusa

### 3.1 Rendelési és lemondási határidő — 3 munkanap, 11:00

Ugyanaz a határidő-függvény kapuzza a **rendelést** és a **lemondást** — ezért `ChangeDeadline` a neve,
nem `CancellationDeadline`. A „3 munkanap" és a „11:00" **nem C# konstans**, hanem az `AppSetting`
(2. fejezet) `ChangeDeadlineWorkingDays` / `ChangeDeadlineLocalTime` mezője — a `WorkingDayCalculator`
ezeket olvassa be, admin futásidőben módosíthatja őket.

```
IsWorkingDay(d)  = d.DayOfWeek ∉ {Sat, Sun} ÉS d ∉ excludedDays

ChangeDeadline(serviceDate):
    d = serviceDate;  counted = 0
    while counted < settings.ChangeDeadlineWorkingDays:
        d = d.AddDays(-1)
        if IsWorkingDay(d): counted++
    return d.ToDateTime(settings.ChangeDeadlineLocalTime)          // helyi idő

CanChange(serviceDate, now) =
        now <= ChangeDeadline(serviceDate)
    ÉS  nincs KitchenClosure(serviceDate)
```

Példa: csütörtöki menü → szerda(1), kedd(2), hétfő(3) → **hétfő 11:00** a határidő.
A visszaszámlálás a kizárt napokat átugorja, így a határidő mindig munkanapra esik.

A `KitchenClosure` az „összesítő elküldve" esemény (`CloseDayCommand`). Ez a második feltétel adja a
gyakorlati viselkedést:

```
hétfő 09:00, semmi nincs lezárva
    csütörtök  rendelhető és lemondható   (ChangeDeadline = hétfő 11:00, még nem járt le)

hétfő 09:00, a csütörtöki összesítő már elment
    csütörtök  KIESIK                      (KitchenClosure)
    péntek     rendelhető és lemondható   (ChangeDeadline = kedd 11:00, még a jövőben)
```

A `ReopenDayCommand` a zárolást feloldja, és ha a 3 munkanapos határidő még nem járt le, a nap újra
elérhetővé válik.

A szabályból következik, hogy **aznapi rendelés és aznapi lemondás sem létezik** — ezt sem a
felhasználó, sem az admin nem tudja kikerülni, és nincs is rá use case.

#### A rendelés két fázisa

Az `OrderDeadline` **nem zárja be** a leadást, csak szűkíti:

| Fázis | Mikor | Mely napokra lehet rendelni |
|---|---|---|
| **A — időszaki (bulk)** | `IsOpen` ÉS `now <= OrderDeadline` | az időszak **bármely** napja (munkanap, nincs kizárva, van publikált menü, nincs lezárva). Átfutási követelmény nincs — ez a „teljes hónapot egyszerre" ablak |
| **B — pótlólagos** | `IsOpen` ÉS `now > OrderDeadline` ÉS `Today <= EndDate` | csak amire `CanChange(nap, now)` áll — de **akár egyszerre az összesre** |

A B fázis az, ami miatt a szabadságról visszatérő vagy a hónap közben belépő kolléga nem marad le egy
egész hónapról: a maradék időszakra egyetlen hívással rendelhet.

**Lemondani mindkét fázisban** a `CanChange` szerint lehet — a bulk ablak erre nem ad kedvezményt.

Szemléletesen: a határidő után egy 3 munkanapos „fal" gördül végig a naptáron, előtte a lezárt napok
esnek ki, mögötte minden nap szabadon rendelhető és lemondható az időszak végéig.

### 3.2 Menüvariáns módosítása / törlése → átvezetés + értesítés

```
DeleteVariant(date, code):
    ha KitchenClosure létezik date-re → Result.Failure("A nap már le van zárva")
    érintett = aktív MenuOrder-ek date-re, MenuVariantId == törlendő
    maradék = a nap többi variánsa, SortOrder majd Code szerint
    ha maradék üres:
        minden érintett rendelés → lemondás (CancellationReason = VariantRemoved)
                                 + jóváírás (3.3) + értesítés (MenuCancelled)
    különben:
        cél = maradék.First()                        // „A menü legyen a default"
        minden érintett rendelés:
            ReassignedFromVariantCode = régi kód;  MenuVariantId = cél.Id;  ReassignedAtUtc = most
            értesítés (OrderReassigned) a rendelés tulajdonosának
            + ha PlacedByUserId ≠ UserId, a leadónak is
    variáns soft delete (RemovedAtUtc = most)                    // 2. fejezet, „miért nincs fizikai törlés"
```

Ugyanez az algoritmus fut le `UpsertDailyMenuCommand`-nál is minden olyan kódra, ami a korábbi
variánslistában megvolt, de az új kérésből hiányzik — egy kódváltoztatás (`"A"` → `"D"`) tehát a régi kód
eltűnéseként és egy új megjelenéseként viselkedik, **nem** átnevezésként. A handler ezért két menetben
menti a változást: előbb `SaveChangesAsync` az új/módosított variánsokra (hogy a friss sorok valódi
`Id`-t kapjanak), utána fut az átvezetés — enélkül egy frissen felvitt variáns nem lehetne érvényes
átvezetési cél.

Puszta **módosításnál** (név/leírás változik) nincs átvezetés, csak `MenuChanged` értesítés az adott nap
aktív rendelőinek. Ha egy `UpsertDailyMenuCommand` hívás egyszerre módosít is és variánst is töröl, egy
rendelés csak **egy** értesítést kap: aki már `OrderReassigned`/`MenuCancelled` üzenetet kapott a fenti
ágban, az nem kap még `MenuChanged`-et is ugyanarra a hívásra — a kettő ugyanazt az eseményt jelentené
kétszer.

### 3.3 Jóváírás: egyenleg és beszámítás

**Keletkezés** — sikeres lemondáskor (vagy nap kizárásakor / menü törlésekor):
```
order.Status = Cancelled
order.CancelledAtUtc / CancelledByUserId / CancellationReason kitöltve
CreditEntry {
    AmountHuf         = +order.PriceHuf
    Kind              = CancellationCredit
    SourceMenuOrderId = order.Id
    RemainingHuf      = order.PriceHuf
}
értesítés (CreditIssued)
```

**Egyenleg** — `Balance(user) = Σ CreditEntry.RemainingHuf`. A jóváírás a keletkezés pillanatától
felhasználható; **nincs `EligibleFrom` várakozási idő**. A felhasználó a felületen egy élő egyenleget lát,
nem egy „majd jövő hónapban" ígéretet.

**Beszámítás** — a legközelebbi olyan időszaki számlánál, amelyen **van menütétel**
(`GeneratePeriodInvoicesCommand(periodId)`):
```
MenuGross     = az időszak aktív MenuOrder-einek PriceHuf összege
                (WHERE OrderingPeriodId = periodId)
ALaCarteGross = az időszak a la carte összege

elérhető  = CreditEntry-k ahol RemainingHuf > 0, rendezve CreatedAtUtc szerint (FIFO)
fedezetlen = MenuGross                        // ← kizárólag a menü rész

minden c ∈ elérhető, amíg fedezetlen > 0:
    fel = min(c.RemainingHuf, fedezetlen)
    c.RemainingHuf -= fel;  fedezetlen -= fel
    CreditEntry { AmountHuf = -fel, Kind = CreditApplied,
                  ConsumesCreditEntryId = c.Id, PeriodInvoiceId = invoice.Id }

invoice.CreditAppliedHuf   = Σ fel
invoice.MenuPayableHuf     = MenuGross - CreditAppliedHuf
invoice.ALaCartePayableHuf = ALaCarteGross
invoice.PayableHuf         = MenuPayableHuf + ALaCartePayableHuf
értesítés (CreditApplied) a levont tételek felsorolásával
```

**Miért nem keveredhet a menü és a la carte.** A lemondott menüadag a konyha szempontjából átütemezés:
az az adag nem készül el, a helyette rendelt *menüadag* váltja ki. Az a la carte külön elszámolás, oda
a menüből származó jóváírás nem folyhat át. Ezért a beszámítás felső korlátja a `MenuGrossHuf`, és a
számla két fizetendő sort mutat. Ha az egyenleg meghaladja az időszak menütételeinek összegét, a
maradék `RemainingHuf`-ban görgetődik tovább a következő olyan **időszakra**, amelyben van menürendelés.

**Időzítés.** Mivel nincs `EligibleFrom`, a beszámítás annál a számlánál történik, amelyik előbb
legenerálódik: ha egy „aug. 5. – szept. 5." időszakon belüli lemondás még ennek az időszaknak a számlája
*előtt* történik, már ebből a menüösszegből levonódik; ha utána, akkor a következő időszakéból. Ez a
„ha előbb jön rá menürendelés, abból vonódjon le" szabály.

**Visszavonás** — ha egy jóváírás alapja megszűnik (kizárás visszavonása, 3.7), a ledger append-only
marad, nem törlünk:
```
CreditEntry { AmountHuf = -eredeti.AmountHuf, Kind = CreditRevoked,
              ConsumesCreditEntryId = eredeti.Id, Note = "Kizárás visszavonva" }
eredeti.RemainingHuf = 0
```
A `GetMyCreditLedgerQuery` ezt is megjeleníti — különben a felhasználó szempontjából indoklás nélkül
csökkenne az egyenlege.

### 3.4 A la carte készletfoglalás versenyhelyzetben

Nem read-then-write, hanem **egyetlen atomi feltételes UPDATE** foglalásonként:

```csharp
var reserved = await db.ALaCarteDailyOffers
    .Where(o => o.Id == offerId && o.OrderedCount < o.Capacity)
    .ExecuteUpdateAsync(s => s.SetProperty(o => o.OrderedCount, o => o.OrderedCount + 1), ct);
// reserved == 0  →  elfogyott
```

Az egész rendelés egy explicit tranzakcióban fut: ha bármelyik sor foglalása 0-t ad vissza, rollback, és a
teljes rendelés hibával tér vissza (részleges rendelés nem keletkezik). Így két felhasználó nem tudja
ugyanazt az utolsó adagot megkapni, és `rowversion` oszlopra sincs szükség — ami azért fontos, mert a
tesztekben használt SQLite nem támogatja.

**Leves kivétel.** A fenti atomi UPDATE csak Főétel/Köret/Desszert/Öntet kategóriájú
`ALaCarteDailyOffer` sorra fut. Leves kategóriájú ajánlatra a rendelés-leadás **nem hajt végre
foglalást és nem is fogad el közvetlen rendelést** — a `PlaceALaCarteOrderCommand` a leves
`ALaCarteItem.PriceHuf`-ját csak a Főétel-sor kombinált árához olvassa ki (2. fejezet), `OrderedCount`-ot
nem növeli. Emiatt egy dolgozó tetszőleges számú *különböző* főételt rendelhet ugyanarra a napra (AC
4.2.3 — tételenként, nem kategóriánként 1 db) anélkül, hogy a leves valaha „elfogyna".

### 3.5 Határidő-ellenőrzések

| Művelet | Feltételek |
|---|---|
| Menürendelés — **A fázis** (bulk) | az `OrderingPeriod` létezik, `IsOpen`, `now <= OrderDeadline`; minden rendelt nap a `[StartDate, EndDate]` tartományban van, munkanap, nincs kizárva, van publikált `DailyMenu`, nincs lezárva; az adott napra nincs már aktív rendelés. **Átfutási követelmény nincs** |
| Menürendelés — **B fázis** (pótlólagos) | ugyanaz, plusz `CanChange(Date, now)` (3.1) minden napra. `now > OrderDeadline`, `Today <= EndDate`. Több nap egyszerre is |
| Menürendelés lemondása | `CanChange(Date, now)` (3.1) → **aznapra sosem teljesül**; a rendelés `Active`. Több nap egyszerre is |
| A la carte rendelés | `Date == ma`; **ma beleesik valamely `OrderingPeriod` `[StartDate, EndDate]` tartományába** (az `IsOpen` és az `OrderDeadline` itt **nem** feltétel — ez aznapi vásárlás, nem előrendelés); `now.TimeOfDay <= 10:30`; ma munkanap és nincs kizárva; minden **nem Leves** tételre van napi ajánlat szabad kerettel; tételenként legfeljebb 1 db és még nem rendelte; **Leves kategóriájú ajánlatra közvetlen rendelés elutasítva** |
| Napi ajánlat rögzítése — Leves | naponta legfeljebb egy aktív Leves ajánlat lehet; egy második felvétele ugyanarra a napra elutasításra kerül |
| A la carte lemondás | **nincs ilyen use case** — a döntés szerint nem törölhető |
| Nap kizárása | `Date > ma` (3.6) — aznapi és múltbeli nap nem zárható ki |
| Kizárás visszavonása | `Date > ma`; a napra nincs `KitchenClosure` (3.7) |
| Időszak felvitele / módosítása | `StartDate <= EndDate`; **`OrderDeadline <= ChangeDeadline(StartDate)`** (a 2. fejezet 2. invariánsa — nem elég a `OrderDeadline < StartDate`, különben az első napra a bulk ablakban még lehetne rendelni, de már nem lehetne lemondani); nem fed át meglévő időszakkal; **a határok csak addig módosíthatók, amíg nincs hozzá rendelés** — utána már csak `Name` / `IsOpen` (2. fejezet, `OrderingPeriod`) |

#### Köteges rendelés és lemondás — részleges siker

Rendelni és lemondani is **több napra egyszerre** lehet. Mivel a napok külön-külön bukhatnak el a
határidőn, a parancs nem mindent-vagy-semmit, hanem **naponkénti eredményt** ad:

```
Result<BatchOrderResult> {
    Succeeded : [ { Date, VariantCode } ]
    Skipped   : [ { Date, Reason } ]      // ErrorCodes, nem szabad szöveg
}
```

A kihagyás okai (`ErrorCodes`): `DeadlinePassed`, `DayClosed`, `DayExcluded`, `NotWorkingDay`,
`MenuNotPublished`, `OutsidePeriod`, `AlreadyOrdered` / `NoActiveOrder`.

A sikeres napok **egy tranzakcióban** mennek be; a kihagyás nem hiba, nem rollbackol. Enélkül a
„szabadságról visszajöttem, megrendelem a hónap maradékát" eset használhatatlan lenne: a lista elején
álló néhány közeli nap elbuktatná a mögötte lévő három hetet.

> **Kötelező szabály a felületre:** ha a `Skipped` nem üres, a művelet **nem jelenthető sikeresnek**.
> Nem elég a létrejött rendeléseket visszaadni — a kihagyott napokat és az okukat mindig meg kell
> jeleníteni, különben a felhasználó abban a hitben marad, hogy mindenre rendelt. A tesztterv külön
> esettel fedi.

### 3.6 Nap kizárása

`ExcludeDayCommand` **csak jövőbeli napra** fogadható el (`Date > Today`). Ennek oka, hogy aznapi
lemondás nincs a rendszerben: a mai napon már futhatnak a la carte rendelések, amikre nincs sztornó
use case, és a konyha is dolgozik. Aznapi üzemzavart a rendszeren kívül kell kezelni (11/8).

**Ez az eszköz veszi át a határmódosítás szerepét,** amint az időszakhoz rendelés tartozik. Tipikus eset:
egy ledolgozós szombat benne van az időszakban és meg is volt nyitva, de kevés létszám miatt mégsem lesz
munkavégzés, így az étkezde sem nyit ki. Az admin nem az időszak `EndDate`-jét tolja el — kizárja azt az
egy napot, és a rendszer az aznapra rendelőknek lemondja a rendelését teljes jóváírással. Ugyanez
működik az időszak **utolsó** napjára is.

Ha a kizárt napra vannak aktív menürendelések, **a lemondási határidőtől függetlenül** mind lemondásra
kerül:

```
ExcludeDay(date, reason):
    ha date <= Today            → Result.Failure("Csak jövőbeli nap zárható ki")
    ha KitchenClosure(date)     → Result.Failure("A nap már le van zárva")
    excluded = új ExcludedDay { Date, Reason, CreatedByUserId }
    minden aktív MenuOrder date-re:
        Status = Cancelled
        CancellationReason = DayExcluded
        CancelledByExcludedDayId = excluded.Id
        jóváírás (3.3) + értesítés (MenuCancelled)
```

**A la carte:** nincs teendő. A tételek csak aznapra rendelhetők, jövőbeli napra tehát nem létezhet
a la carte rendelés, a `GetDailyOffersQuery` pedig a kizárt napokra üres listát ad — az ajánlatok
maguktól láthatatlanná válnak, visszavonáskor pedig újra megjelennek. Ugyanez vonatkozik a Leves
ajánlatra is: kizárt napra sem főétel, sem leves nem rendelhető, hiszen a la carte rendelés csak
aznapra létezhet.
*(Az eredeti terv „az adott napi ajánlatok inaktiválódnak" mondata törölve: nincs ilyen mező az
`ALaCarteDailyOffer`-en, és nincs is rá szükség.)*

### 3.7 Kizárás visszavonása

A `RemoveExcludedDayCommand` nem tudja egyszerűen visszafordítani a 3.6-ot, mert a lemondás **jóváírást
is keletkeztetett**, ami azóta már elköltődhetett. Ezért **feltételes, rendelésenkénti** visszaállítás,
`bool RestoreCancelledOrders` kapcsolóval.

Egy rendelés csak akkor áll vissza, ha **mind** teljesül:

1. `CancellationReason == DayExcluded` **és** `CancelledByExcludedDayId == a most visszavont kizárás`
   (a felhasználó saját lemondása tehát **soha** nem éled újra)
2. a hozzá tartozó `CreditEntry.RemainingHuf == AmountHuf` — érintetlen, nincs rá `CreditApplied` tétel
3. a rendelés `OrderingPeriodId`-jára még nincs `PeriodInvoice` generálva
4. a napra nincs `KitchenClosure`
5. a felhasználónak nincs időközben új aktív rendelése arra a napra
   (különben a szűrt unique index amúgy is elhasalna)

```
RemoveExcludedDay(date, restoreCancelledOrders):
    ha date <= Today          → Result.Failure("Csak jövőbeli nap kizárása vonható vissza")
    ha KitchenClosure(date)   → Result.Failure("A nap már le van zárva")
    excluded = ExcludedDay(date)
    ExcludedDay törlése
    ha nem restoreCancelledOrders → return

    jelöltek = MenuOrder-ek ahol CancelledByExcludedDayId == excluded.Id
    minden o ∈ jelöltek:
        ha a fenti 2–5. feltétel mind teljesül:
            o.Status = Active
            o.CancelledAtUtc / CancelledByUserId / CancellationReason /
              CancelledByExcludedDayId = null
            jóváírás visszavonása (CreditRevoked, 3.3)
            értesítés (OrderRestored)
        különben:
            marad lemondva
            értesítés (DayReopened) — „a nap mégis kiszolgálásra kerül, a jóváírásod megmarad,
            a leadási határidőn belül újra rendelhetsz"
```

A command `Result<RemoveExcludedDayResult>`-ot ad vissza: `RestoredCount`, `SkippedCount` és a kihagyás
okainak bontása, hogy az admin lássa, mi történt.

**Miért nem elég a „mondja le és rendeljen újra a felhasználó" megközelítés:** a kizárás visszavonása
tipikusan az időszak közben, a leadási határidő után történik, amikor a `OrderingPeriod` már nem `IsOpen` —
a felhasználó tehát nem tudna újrarendelni.

### 3.8 Munkanap-számítás és kizárt napok kölcsönhatása

Egy kizárt nap felvitele visszamenőleg **korábbra tolja** más napok törlési határidejét (3.1), mert a
visszaszámlálás átugorja. Az érintett nap saját rendeléseit automatikusan lemondjuk (3.6); a többi nap
határidő-eltolódását elfogadjuk, külön kezelés nélkül.

---

## 4. Időkezelés

- `builder.Services.AddSingleton(TimeProvider.System)` — a keretrendszer beépített absztrakciója.
- Fölé egy vékony `IAppClock`: `DateTimeOffset UtcNow`, `DateTime LocalNow`, `DateOnly Today`,
  `DateTime ToLocal(DateTimeOffset)`. A helyi zóna **Europe/Budapest**
  (`TimeZoneInfo.FindSystemTimeZoneById("Europe/Budapest")` — .NET 6+ óta Windowson is működik az IANA id).
- Minden határidő (11:00, 10:30, `OrderDeadline`) **helyi időben** értelmezett; minden tárolt időpillanat
  UTC-ben megy az adatbázisba.
- Tesztben `Microsoft.Extensions.TimeProvider.Testing` → `FakeTimeProvider`, így a 10:59 / 11:01 és a
  10:29 / 10:31 határesetek determinisztikusan tesztelhetők.

---

## 5. Aktuális felhasználó — dev felhasználóváltás

Globális `InteractiveServer` mellett a prerender és a circuit **külön DI scope**, ezért egy egyszerű
scoped „current user" mező nem elég. Megoldás: **valódi cookie authentication, fake bejelentkezéssel**.

A cookie melletti döntő érv a fejlesztői munkafolyamat: a Data Protection kulcsok lemezre kerülnek
(`%LOCALAPPDATA%\ASP.NET\DataProtection-Keys`), így a süti **túléli az alkalmazás újraindítását** és az
F5-öt is. Egy memóriában tartott `AuthenticationStateProvider` minden gyakori újraindításnál
újrabejelentkezést követelne.

- `AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(...)`,
  `ExpireTimeSpan = 30 nap`, `SlidingExpiration = true`
- Minimal API endpointok: `POST /dev-login` (form, `userName` mező) → `Users` táblából kikeresi,
  `HttpContext.SignInAsync` a `NameIdentifier` (= `User.Id`), `Name` (= `UserName`) és `Role`
  claimekkel; `POST /dev-logout` → `SignOutAsync`. Mindkettőn `.DisableAntiforgery()`.
- `builder.Services.AddCascadingAuthenticationState()`, `AddAuthorizationBuilder()` `"Admin"` policyvel.
- `ICurrentUser` (scoped) az `AuthenticationStateProvider`-ből olvas — prerenderben és circuitben
  egyaránt működik.
- **Buktató:** `SignInAsync` valódi HTTP kérést igényel, interaktív circuitből nincs `HttpContext`.
  A váltó felület ezért **form POST** legyen az endpointra (nem `@onclick` + service hívás).
- **Buktató:** a sütiben tárolt `User.Id` elavulhat adatbázis-újragenerálás után. `OnValidatePrincipal`
  eseményben ellenőrizzük, hogy a felhasználó létezik-e, különben `RejectPrincipal()` + `SignOutAsync`.
- Előny: amikor jön az igazi hitelesítés, csak a `/dev-login` endpoint cserélődik — a
  `[Authorize(Roles="Admin")]` és minden más marad.

---

## 6. MediatR use case-ek

Jelölés: **[A]** = admin, **[U]** = felhasználó.

> Az itt felsorolt use case-ek **user story és elfogadási kritérium** megfelelője a
> `02-user-stories.md`-ben van; annak záró **lefedettségi mátrixa** köti össze a két dokumentumot.
> Ha ez a lista változik, a mátrixot is frissíteni kell.

### Users
- `GetUsersQuery` **[A/U]** — felhasználólista (admin nézet és a dev váltó); a valós implementáció
  (`Features/Users/GetUsers/`) a Billing kézi jóváírás autocomplete-jéhez készült el elsőként, két extra
  mezővel (`Igazgatosag`, `Osztaly`) a névsor-egyértelműsítéshez — a dev váltó (`StubCurrentUser`) és a
  "más nevében rendelek" választó (`UserCalendar.razor`, `colleagues`) egyelőre saját, korábbi
  implementációt használ, nincs átvezetve erre.
- `GetUserByIdQuery`, `GetUserByUserNameQuery` **[A/U]**

### Calendar
- `UpsertOrderingPeriodCommand` **[A]** — időszak felvitele/módosítása: `Name`, `StartDate`, `EndDate`,
  `OrderDeadline`, `IsOpen`. Érvényesíti a 2. fejezet négy invariánsát, az átfedés-ellenőrzést
  serializable tranzakcióban. Ha az időszakhoz már tartozik rendelés, a dátummezők módosítása
  elutasításra kerül — onnantól a nap-szintű korrekció az `ExcludeDayCommand` (3.6)
- `GetOrderingPeriodQuery`, `GetOrderingPeriodsQuery` **[A/U]**
- `GetOrderingPeriodForDateQuery` **[A/U]** — dátum → időszak feloldás, vagy „a nap nincs lefedve"
- `GetUncoveredWorkdaysQuery` **[A]** — egy dátumtartomány azon munkanapjai, amelyek egyetlen időszakba
  sem esnek (a rés-riport)
- `ExcludeDayCommand` **[A]** — nap kizárása (csak jövőbeli nap); 3.6 szerint lemondja az érintett
  rendeléseket `DayExcluded` okkal
- `RemoveExcludedDayCommand` **[A]** — kizárás visszavonása `RestoreCancelledOrders` kapcsolóval;
  3.7 szerint feltételesen visszaállítja a *kizárás miatt* lemondott rendeléseket, a jóváírást
  `CreditRevoked` tétellel visszavonja, és visszaadja a visszaállított/kihagyott bontást
- `GetExcludedDaysQuery` **[A/U]** — időszakra
- `GetOrderableDaysQuery` **[U]** — **a felület egyetlen igazságforrása**: az időszak minden napjára
  megmondja, hogy *rendelhető-e*, *lemondható-e*, és ha nem, **miért** (ugyanaz az `ErrorCodes` készlet,
  amit a köteges parancsok `Skipped` listája használ). Így a felület előre le tudja tiltani a nem
  választható napokat, és a parancs eredménye megerősítés, nem meglepetés

### Menus
- `UpsertDailyMenuCommand` **[A]** — nap menüjének létrehozása/módosítása variánsokkal; a kikerült
  variánsok átvezetése + értesítés (3.2). Ha a `Date`-re korábban `DeleteDailyMenuCommand` fut le
  (soft-deletelt sor), a hívás **felélezti** a régi `DailyMenu`/`MenuVariant` sorokat ahelyett, hogy
  újat szúrna be — az unique indexek (`Date`, `(DailyMenuId, Code)`) ezt megkövetelik (2. fejezet).
  Nincs külön publikálás-lépés: minden sikeres mentés (a validáció már megköveteli legalább egy
  variáns meglétét) azonnal `IsPublished = true`-ra állítja a napot — korábban ezt egy külön
  `PublishDailyMenuCommand` végezte, ez a use case megszűnt
- `DeleteMenuVariantCommand` **[A]** — egy variáns **soft delete**-je (`RemovedAtUtc`), átvezetés az
  első maradék variánsra vagy lemondás, ha nem marad
- `DeleteDailyMenuCommand` **[A]** — teljes nap menüjének **soft delete**-je, minden aktív rendelés
  lemondása (`MenuDeleted`) + jóváírás; a menü és minden variánsa `RemovedAtUtc`-t kap,
  `IsPublished = false`
- `GetDailyMenuQuery` **[A/U]**, `GetPeriodMenuQuery` **[A/U]** — az időszaki rendelőfelület adata;
  mindkettő a `RemovedAtUtc == null` variánsokat/napokat adja vissza, és az `IncludeUnpublished` flaget
  a hívó szerepköre szerint kell állítani ([A] = true, [U] = false — AC 2.5.2)
- `GetTodayMenuForUserQuery` **[U]** — **a „napi kiírás"**: mai menü variánsai + a felhasználó aznapi
  választása, vagy explicit „ma nem rendeltél" jelzés + mai a la carte kínálat és a felhasználó a la
  carte rendelése
- `GetMenuDishSuggestionsQuery` **[A]** — a `MenuDish` katalógus (2. fejezet) leves/főétel neve +
  allergénje + tápértéke, kind szerint kettébontva; az admin felület autocomplete-forrása
- `CreateMenuDishCommand` **[A]** — új leves/főétel felvétele a `MenuDish` katalógusba, allergénekkel
  és a 7 tápérték-mezővel; az "+ Új étel" dialógus (`AddMenuDishDialog`) forrása. A napi menü szerkesztő
  dialógus (`EditDailyMenuDialog`) ezt **nem** hívja — az csak a katalógusból választ (2. fejezet,
  `MenuDish`); az admin felületi bekötés (honnan nyitható meg ez a dialógus) nyitott teendő
- `UpdateMenuDishCommand` **[A]** — meglévő katalógustétel nevének, allergénjeinek és tápértékének
  módosítása

### Orders
- `PlacePeriodOrderCommand` **[U]** — `TargetUserId` + `OrderingPeriodId` + `(Date, VariantCode)` lista;
  minden dátum az időszak `[StartDate, EndDate]` tartományába kell essen, és a rendelésekre rákerül az
  `OrderingPeriodId`. A fázistól függően a 3.5 A vagy B sorát érvényesíti napokra bontva, és
  `Result<BatchOrderResult>`-ot ad vissza a `Skipped` listával. A más nevében rendelést a
  `TargetUserId ≠ CurrentUser` eset fedi, `PlacedByUserId` mindig az aktuális felhasználó.
- `CancelMenuOrdersCommand` **[U]** — `TargetUserId` + **dátumlista**; naponként `CanChange` (3.1),
  `CancellationReason = ByUser`, jóváírás létrehozása, értesítés. Szintén `Result<BatchOrderResult>`.
  Egyetlen nap lemondása ennek az egyelemű esete — nincs külön parancs rá.
- `GetMyPeriodOrderQuery` **[U]**, `GetUserOrdersQuery` **[A]** (szűrők: időszak, felhasználó, státusz)

### ALaCarte
- `UpsertALaCarteItemCommand` **[A]**, `SetALaCarteItemActiveCommand` **[A]** (kétirányú — kivezetés
  ÉS visszaaktiválás egyaránt ezt hívja), `GetALaCarteItemsQuery` **[A]** — az 5 kategória
  (`Leves`/`Foetel`/`Koret`/`Desszert`/`Ontet`) egyikéhez tartozó törzsadat, a `MenuDish` mintáját
  követő 7 tápérték-mezővel (2. fejezet)
- `SetDailyOfferCommand` **[A]** — napi keret beállítása (`Capacity` nem csökkenthető a lefoglalt alá);
  **Leves kategóriájú tételre naponta legfeljebb egy aktív ajánlat rögzíthető** — egy második felvétele
  ugyanarra a napra elutasításra kerül
- `RemoveDailyOfferCommand` **[A]** — csak ha még nincs rá rendelés
- `GetDailyOffersQuery` **[A/U]** — szabad darabszámmal; kizárt napra üres lista. **A Leves ajánlat nem
  jelenik meg önálló, rendelhető sorként** — a „szabad" darabszám rá nézve nem értelmezett
- `PlaceALaCarteOrderCommand` **[U]** — 10:30 határidő + atomikus foglalás (3.4) **a Leves kivételével**;
  **Leves kategóriájú ajánlatra közvetlen rendelés elutasítva** (`Result.Failure`); Főétel-sor
  `UnitPriceHuf`-ja a főétel árának és az aznapi Leves-ajánlat árának összege (0, ha nincs Leves-ajánlat),
  és a sor `IncludesSoup` mezője ekkor snapshotolódik; a mai napot lefedő időszakot feloldja, és
  `OrderingPeriodId`-ként rögzíti — lefedetlen napon a rendelés elutasítva
- `GetALaCarteDailySummaryQuery` **[A]** — aznapi konyhai lista tételenként, kategóriánként
  csoportosítva; a levesadag-szám **levezetett** érték: az aznapi `CategorySnapshot == Foetel` sorok
  darabszáma
- `GetALaCarteMonthlySummaryQuery` **[A]** — ugyanez a tételenkénti/levesadag-összesítés, egy teljes
  hónap napjaira összevonva (visszamenőleges rendelési igény kiszolgálására)

### Kitchen
- `GetKitchenSummaryQuery` **[A]** — egy napra, variánsonkénti darabszám (élő)
- `GetKitchenSummaryRangeQuery` **[A]** — időszakra, ez adja a „3 nappal előbbi" megrendelés alapját
- `CloseDayCommand` **[A]** — **az „összesítő elküldve" esemény**: snapshot mentése
  `KitchenClosure`(+`Line`) táblába, és ettől kezdve a nap kiesik a rendelhető/lemondható körből (3.1),
  akkor is, ha a 3 munkanapos határidő még nem járt le
- `ReopenDayCommand` **[A]** — zárolás feloldása (a snapshot megmarad auditnak); ha a határidő még nem
  járt le, a nap újra rendelhetővé és lemondhatóvá válik
- `GetKitchenClosureQuery` **[A]**

### Billing
- `GeneratePeriodInvoicesCommand(OrderingPeriodId)` **[A]** — az időszak számláinak generálása +
  jóváírás FIFO beszámítás **kizárólag a menürészre** (3.3)
- `MarkInvoicePaidCommand` **[A]** — **a kézi fizetés-jelölés**
- `GetInvoicesQuery` **[A]** (`OrderingPeriodId` + fizetett szűrő), `GetMyInvoicesQuery` **[U]**
- `GetMyBalanceQuery` **[U]** — az aktuális egyenleg (`Σ RemainingHuf`) a fejlécbe/dashboardra
- `GetMyCreditLedgerQuery` **[U]** — az átlátható jóváírás-kimutatás, a `CreditRevoked` tételekkel együtt
- `AddManualCreditCommand` **[A]** — kézi korrekció indoklással (szintén menü-hatókörű)
- `GetBalancesQuery` **[A]** — minden dolgozó aktuális, nem-nulla egyenlege (`Σ RemainingHuf`
  felhasználónként), az admin "Egyenlegek" áttekintő oldalához; nincs önálló user story, AC 5.2.1-et
  támogató implementációs részlet

### Notifications
- `GetMyNotificationsQuery` **[U]**, `MarkNotificationReadCommand` **[U]**,
  `MarkAllNotificationsReadCommand` **[U]**

**Kimenet-konvenció:** minden command `Result` / `Result<T>` értéket ad vissza (nem kivételt) a várt
üzleti kimenetekre (határidő lejárt, elfogyott, nap lezárva). A `ValidationBehavior` a bemeneti
alakhelyességet ellenőrzi FluentValidationnel és `ValidationException`-t dob — az valóban programhiba.

---

## 7. DI regisztráció

`Extensions/` alatt csoportosított extension metódusok (a `microsoft-extensions-dependency-injection`
skill mintája szerint), hogy a `Program.cs` olvasható maradjon és a tesztek ugyanazt hívhassák:

```csharp
builder.Services.AddEbedrendeloData(builder.Configuration);   // AddDbContextFactory<EbedrendeloDbContext>
builder.Services.AddEbedrendeloTime();                        // TimeProvider.System + IAppClock
builder.Services.AddEbedrendeloApplication();                 // MediatR + FluentValidation + behavior + domain szolgáltatások
builder.Services.AddDevAuthentication();                      // cookie auth + policy + ICurrentUser + CascadingAuthenticationState
```

a pipeline-ban `app.UseAuthentication(); app.UseAuthorization();` az `UseAntiforgery()` **elé**, és
`app.MapDevAuthEndpoints();`.

**`AddDbContextFactory`, nem scoped `AddDbContext`** — Blazor Serverben a circuit-scope hosszú életű és
párhuzamos műveletek futhatnak rajta; a handlerek `IDbContextFactory<EbedrendeloDbContext>`-ből kérnek
saját kontextust műveletenként. Egy handler = egy unit of work.

Connection string (`appsettings.json`):
`Server=(localdb)\\MSSQLLocalDB;Database=EbedrendeloApp;Trusted_Connection=True;TrustServerCertificate=True`

---

## 8. Seed / init adat

`Data/Seed/DatabaseSeeder.cs`, indításkor hívva (`await db.Database.MigrateAsync()` majd idempotens
feltöltés — minden blokk csak akkor fut, ha az adott tábla/nap még üres):

- **6 felhasználó**: `admin` (Role=`Admin`) + 5 dolgozó (`kovacs.j`, `nagy.a`, `szabo.p`, `toth.e`,
  `varga.b`), `UserId` 1001–1006, kitöltött `Nev` / `Igazgatosag` / `Osztaly` / `Rf` / `SzervKod`.
- **OrderingPeriod**: **két, egymáshoz csatlakozó, szándékosan nem naptári időszak** — az aktuális hónap
  5-étől a következő hónap 5-éig, majd onnan az azt követő hónap 5-éig. `OrderDeadline` =
  `StartDate − 10 nap` 10:00, `IsOpen = true`. Így a seed maga demonstrálja, hogy a „hónap" eltolható.
- **ExcludedDay**: 1–2 minta jövőbeli napra (pl. „Karbantartás").
- **DailyMenu + MenuVariant**: a két időszak által lefedett **minden munkanapra** A/B/C variáns,
  publikálva — kódból generálva egy ~15 elemű recept-katalógusból (`SeedCatalog`), nem kézi felsorolással.
- **ALaCarteItem**: 13 tétel — 1 leves (Csontleves), 5 főétel, 4 köret, 2 desszert, 1 öntet (Tartár
  mártás), egyedi árakkal; a 7 tápérték-mező üresen marad (jövőbeli admin-feladat).
- **ALaCarteDailyOffer**: a következő 5 munkanapra, tételenként napi keret — a Leves tételnek is jár
  napi ajánlat (legfeljebb egy/nap), a `Capacity` rá nézve figyelmen kívül hagyott placeholder
  (`int.MaxValue`), mert a leves korlátlan és sosem kerül ellenőrzésre.
- **Minta forgalom**: néhány aktív `MenuOrder` az **első időszakhoz kötve**, 1 felhasználó által lemondott
  rendelés (`ByUser`) a hozzá tartozó `CreditEntry`-vel, 1 kizárás miatt lemondott rendelés
  (`DayExcluded`), 1–2 `UserNotification` — hogy a ledger, az egyenleg és az értesítés nézet ne legyen üres.

---

## 9. Tesztelési terv

A CLAUDE.md szerint a logika-tesztek bUnit nélkül futnak; bUnit csak a renderelésre kell — a UI a végén jön.

**Tiszta unit tesztek (adatbázis nélkül)**
- `WorkingDayCalculatorTests` — csütörtök → hétfő; hétvége átugrása; kizárt nap átugrása; hónap- és
  évforduló; a határidő mindig munkanapra esik
- `ChangeDeadlineTests` — hétfő 10:59 → csütörtök még változtatható, 11:01 → már nem
  (`FakeTimeProvider`); aznapi rendelés és lemondás soha nem engedélyezett; a kizárt napot a
  visszaszámlálás átugorja
- `CreditApplicationTests` — FIFO sorrend; a jóváírás **azonnal** felhasználható (nincs `EligibleFrom`);
  a beszámítás nem lépheti túl a `MenuGrossHuf`-ot; a la carte összeg érintetlen marad; részleges
  felhasználás; maradék görgetése

**Handler tesztek adatbázissal** — `Microsoft.EntityFrameworkCore.Sqlite` **in-memory** (nyitva tartott
kapcsolat, `DataSource=:memory:`): valódi relációs viselkedés, gyors, támogatja a szűrt indexeket és az
`ExecuteUpdateAsync`-et. Lefedendő:
- napi 1 menü szabály megsértése (szűrt unique index)
- **rendelési időszak** (2. fejezet):
  - átfedő időszak felvitele elutasítva (részleges és teljes átfedés is)
  - `OrderDeadline > ChangeDeadline(StartDate)` elutasítva; a pontosan `ChangeDeadline(StartDate)`-re
    eső határidő viszont még **elfogadott** (a `<=` határeset mindkét irányból tesztelve)
  - rés-napra sem menürendelés, sem a la carte nem adható le
  - rendelés nélküli időszak határai módosíthatók; **az első rendelés után a `StartDate` / `EndDate` /
    `OrderDeadline` módosítása elutasítva**, a `Name` és az `IsOpen` viszont írható marad
  - az utolsó nap kizárása (`ExcludeDayCommand`) az időszak zsugorítása helyett: a rendelések lemondva,
    jóváírva, az időszak határa változatlan
  - a rendelt nap az időszakon kívül → elutasítva
  - `GetUncoveredWorkdaysQuery` a két időszak közti hétköznapot visszaadja, a hétvégét nem
- **rendelési ablakok** (3.1):
  - **A fázis:** a határidő előtt az időszak bármely napjára le lehet adni, átfutási követelmény nélkül
  - **B fázis:** a határidő után csak a `CanChange` szerinti napokra; a 3 munkanapon belüliek kimaradnak
  - **a példa:** hétfőn a csütörtök rendelhető és lemondható; ha a csütörtöki nap le van zárva,
    a csütörtök kiesik és a legkorábbi elérhető nap a péntek
  - `ReopenDayCommand` után a nap újra elérhető, ha a határidő még nem járt le
  - **hónap közben belépő kolléga / szabadságról visszatérő:** a maradék időszakra egyetlen hívással
    rendel; a lezárt és a közeli napok kimaradnak, a többi bemegy
- **köteges művelet részleges sikerrel** (3.5):
  - 16 napos lista, 2 nap kiesik → `Succeeded` 14, `Skipped` 2 a helyes `Reason`-nel
  - a `Skipped` nem üres → **a művelet nem jelenthető sikeresnek**; a válasz tartalmazza minden
    kihagyott nap okát
  - a sikeres napok egy tranzakcióban jönnek létre; a kihagyás nem rollbackol
  - köteges lemondás ugyanezekkel a határesetekkel
  - `GetOrderableDaysQuery` naponkénti `orderable` / `cancellable` + ok egyezik a parancs kimenetével
  - időszakon kívüli nap → `OutsidePeriod`, akkor is, ha egyébként a 3 munkanapon túl van
- más nevében rendelés → `PlacedByUserId` helyesen naplózva
- **étlap- és variánskezelés** (3.2, Menus feature):
  - `UpsertDailyMenuCommand` — új nap létrehozása azonnal publikáltan; lezárt napra elutasítva (`DayClosed`)
  - névre/leírásra korlátozott módosítás nem mozgat és nem mond le rendelést (AC 2.3.1), de az adott nap
    aktív rendelői `MenuChanged` értesítést kapnak
  - új menü létrehozása **nem** küld `MenuChanged`-et (nincs kire, amíg nincs korábbi rendelés)
  - variáns kikerülése (akár `DeleteMenuVariantCommand`-dal, akár `UpsertDailyMenuCommand`-ból való
    kihagyással) → átvezetés a legkisebb `SortOrder`/`Code` szerinti maradék variánsra, `OrderReassigned`
    a tulajdonosnak és — ha eltér — a leadónak is
  - variánskód átnevezése (`"A"` → `"D"`) egyetlen aktív variánson → a rendelés átvezetődik az új kódra,
    nem mondódik le (bizonyítja, hogy a frissen felvitt variáns még ugyanabban a hívásban érvényes
    átvezetési cél)
  - az utolsó variáns törlése/kihagyása → lemondás `VariantRemoved` okkal + jóváírás + `MenuCancelled`
  - `DeleteDailyMenuCommand` → a nap minden aktív rendelése lemondva `MenuDeleted` okkal + jóváírás;
    lezárt napra elutasítva
  - törlés után `UpsertDailyMenuCommand` ugyanarra a `Date`-re **feléleszti** a soft-deletelt sort, nem
    ütközik az unique indexbe, és a felélesztéssel egyben újra publikálttá is teszi
  - `GetDailyMenuQuery`/`GetPeriodMenuQuery` a publikálatlan napot és a soft-deletelt variánst elrejti
    `IncludeUnpublished = false` mellett, de megmutatja `true` mellett
- teljes menü törlése / nap kizárása → lemondás helyes `CancellationReason`-nel + jóváírás + értesítés
- **aznapi vagy múltbeli nap kizárása elutasítva**
- **kizárás visszavonása** (3.7):
  - a felhasználó által lemondott rendelés kizárás + visszavonás után is lemondva marad
  - érintetlen jóváírás → a rendelés visszaáll, `CreditRevoked` keletkezik, az egyenleg 0
  - számlagenerálás után → a rendelés **nem** áll vissza, a felhasználó `DayReopened` értesítést kap
  - a felhasználó időközben újrarendelt → nincs duplikáció, az index nem sérül
  - kétszeres kizárás/visszavonás ciklus → csak a saját `ExcludedDayId`-hoz tartozó rendelések állnak vissza
- a la carte 10:30 határidő; tételenként 1 db; készlet kimerülése
- nap lezárása után a rendelés és a lemondás elutasítása
- számla generálás: menü bruttó, a la carte bruttó, beszámított jóváírás, két fizetendő sor
- számla generálás **`OrderingPeriodId` szerint gyűjt**, nem naptári hónap szerint: egy aug. 5. – szept. 5.
  időszak számlája tartalmazza az augusztus végi *és* a szeptember eleji rendeléseket is

**Integrációs teszt LocalDB-vel** — külön xUnit collection, a párhuzamos készletfoglalásra
(két egyidejű `PlaceALaCarteOrderCommand` az utolsó adagra → pontosan egy siker). Ha a LocalDB nem
elérhető, a collection kihagyódik.

**Teendő a teszt projektben:** `UnitTest1.cs` törlése; `Microsoft.EntityFrameworkCore.Sqlite` **10.0.11**
és `Microsoft.Extensions.TimeProvider.Testing` **10.9.0** hozzáadása; `_Imports.razor` csak akkor, amikor
bUnit tesztek jönnek. (Az xUnit v2 + bUnit 2.9.0 párosítás marad — a CLAUDE.md szerint ez a működő
kombináció.)

---

## 10. Végrehajtási sorrend

Minden fázis végén `dotnet build` és `dotnet test` zölden kell fusson.

**Fázis 0 — build alapok**
`Directory.Build.props` (TargetFramework, Nullable, ImplicitUsings központilag) +
`.config/dotnet-tools.json` a `dotnet-ef` lokális eszközzel (globálisan **nincs telepítve**). A
csomagverziók projektenként, a `.csproj`-okban maradnak (`Version` attribútummal) — **nincs Central
Package Management / `Directory.Packages.props`**, ez szándékos döntés.
EF Core csomagok az app projektbe: `Microsoft.EntityFrameworkCore.SqlServer` **10.0.11** és
`Microsoft.EntityFrameworkCore.Design` **10.0.11** (ellenőrizve a nuget.org-on, ez a legfrissebb 10.x).
*Ellenőrzés:* `dotnet build`, `dotnet test`, `dotnet ef --version`.

**Fázis 1 — adatmodell és adatbázis**
`Domain/Entities` (benne `Role`, `AppSetting`) + `Enums` (`CancellationReason`, bővített `CreditEntryKind`);
`EbedrendeloDbContext` + `Configurations`; connection string; `AddEbedrendeloData`;
`dotnet ef migrations add InitialCreate`; `DatabaseSeeder` + indítási hívás.
*Ellenőrzés:* `dotnet ef database update` lefut, az app elindul, a LocalDB-ben ott a séma és a seed adat.

**Fázis 2 — közös infrastruktúra**
`Result`/`ErrorCodes`; `IAppClock`; `IWorkingDayCalculator`; `ValidationBehavior` + MediatR és
FluentValidation regisztráció; cookie-alapú dev auth + `ICurrentUser` + `/dev-login` endpoint +
`OnValidatePrincipal` ellenőrzés.
*Ellenőrzés:* a munkanap- és határidő-unit tesztek zölden; `/dev-login` beállítja a sütit, és az
újraindítás után is érvényes marad.

**Fázis 3 — naptár és menük (admin oldal logikája)**
Calendar és Menus feature use case-ek, `IMenuReassignmentService`, `INotificationService`,
a 3.6 / 3.7 kizárás–visszavonás páros. Itt készül az **időszak-kezelés** is:
`UpsertOrderingPeriodCommand` az átfedés-ellenőrzéssel, `GetOrderingPeriodForDateQuery`,
`GetUncoveredWorkdaysQuery`.
*Ellenőrzés:* átvezetés-, nap-kizárás- és visszaállítás-handler tesztek; átfedő időszak elutasítva;
a rés-riport a két időszak közti hétköznapot mutatja.

**Fázis 4 — időszaki rendelés és lemondás**
Orders feature a **kétfázisú rendelési ablakkal** (3.1) és a köteges, részleges sikerű parancsokkal
(`PlacePeriodOrderCommand`, `CancelMenuOrdersCommand`); `ICreditService` (jóváírás keletkezés +
visszavonás); a bővített `GetOrderableDaysQuery`.
*Ellenőrzés:* 3 munkanap + 11:00, A és B fázis, lezárt nap kiesése, napi 1 db, más nevében rendelés,
időszakon kívüli nap elutasítva, köteges `Skipped` lista a helyes okokkal.

**Fázis 5 — a la carte**
Törzsadat, napi keret, atomikus foglalás.
*Ellenőrzés:* 10:30 határidő, készlet-kimerülés, párhuzamossági integrációs teszt.

**Fázis 6 — konyha és számlázás**
Kitchen összesítő + `CloseDayCommand`; Billing `GeneratePeriodInvoicesCommand`, jóváírás beszámítás a
menürészre, egyenleg-lekérdezés, fizetés-jelölés.
*Ellenőrzés:* „a jóváírás nem csökkenti az a la carte fizetendőt" teszt; „azonnal beszámítható" teszt;
a számla `OrderingPeriodId` szerint gyűjt (hónaphatáron átnyúló időszakkal); lezárt nap immutábilis.

**Fázis 7 — minimális felület**
`GetTodayMenuForUserQuery` napi kiírás oldal, egyenleg a fejlécben, felhasználóváltó, admin
menüszerkesztő váz. A sablonoldalak (`Counter`, `Weather`, `Home` tartalma) törlése, `NavMenu` átírása,
bUnit tesztek.

---

## 11. Kockázatok és nyitott kérdések

1. **MediatR licenc** — a MediatR 13.0-tól kereskedelmi licencű; belső céges használatnál érdemes
   ellenőrizni, kell-e licenckulcs. Ha nem, a use case szerkezet változatlanul átültethető egy egyszerű
   handler-diszpécserre.
2. **A la carte jóváírás** — jelenleg minden jóváírás menü-hatókörű, mert a la carte lemondás nincs.
   Ha valaha kell a la carte korrekció (elmaradt adag), a `CreditEntry`-re egy `Scope` mező kerül, és a
   beszámítás hatóköre szerint válik szét — a `PeriodInvoice` bontása ezt már ma is elbírja. Ezen felül:
   mivel a Leves ára a Főétel-sor `UnitPriceHuf`-jába van beolvasztva, egy jövőbeli tételes korrekció
   (pl. csak a főételt cserélik, a levest nem) nem vonhatja ki egyszerűen a katalógusárat a
   snapshotból — a bontást a korrekció pillanatában, a két akkori katalógusárból kellene újraszámolni.
3. **`Rf`, `SzervKod` szemantikája** ismeretlen — csak tárolt adat, logika nem épül rá.
4. **Nincs valódi hitelesítés** — az app csak belső hálón futtatható, amíg a dev bejelentkezés él.
5. **Időzóna**: minden határidő Europe/Budapest szerint; ha a szerver más zónában fut, az `IAppClock`
   konverzió a kritikus pont — külön teszt fedi.
6. **Jóváírás felhasználás nélkül kilépő dolgozó** — a maradék `RemainingHuf` kezelése nincs
   specifikálva; egyelőre egyenlegen marad.
7. **Egyenleg fizetetlen számla mellett** — ha a felhasználónak van egyenlege, de egy korábbi számlája
   fizetetlen, a beszámítás akkor is csak az *új* számlára történik; visszamenőleges rendezés nincs.
8. **Aznapi üzemzavar** (áramszünet, konyhaleállás) — a rendszer nem kezeli, mert aznapi lemondás nincs
   és a mai nap nem zárható ki. Ilyenkor kézi `AddManualCreditCommand` az érintetteknek.
9. **Az átfedés-tilalom csak handler-szinten él.** SQL Serverben nincs exclusion constraint, a
   `UQ StartDate` / `UQ EndDate` csak az azonos határú duplikátumot fogja meg. Ha valaki az adatbázison
   kívülről (kézi `INSERT`, migráció) visz be átfedő időszakot, a rendszer nem veszi észre; egy nap két
   időszakhoz tartozna, és a rendelés kiszámíthatatlanul kerülne számlára. Ha ez kockázat, egy
   „nap → időszak" segédtábla `UQ Date`-tel adna adatbázis-szintű garanciát, cserébe napi granularitású
   karbantartásért.
10. **Lefedetlen napon nincs a la carte.** A kötelező `ALaCarteOrder.OrderingPeriodId` miatt rés-napon
    az aznapi vásárlás is elutasításra kerül, holott a konyha akár működhetne. Ha ez zavaró, a mező
    nullable-lé tehető, de akkor kell egy „számlázatlan a la carte tételek" nézet, különben csendben
    elvesznének a bevételből.
11. **Variánsváltás (A → B).** Nincs rá külön use case: a felhasználó lemond és újrarendel, ami két
    ledger-tételt szül (jóváírás + új terhelés). Az összeg helyes, de a kimutatás zajos. Ha zavaró, egy
    `ChangeMenuVariantCommand` egyetlen `MenuVariantId`-frissítéssel elintézné, ledger-mozgás nélkül.
12. **A `CloseDayCommand` bármely jövőbeli napra kiadható**, tehát az admin elvileg a 3 munkanapos ablak
    előtt is lezárhat egy napot, és ezzel korábban elvághatja a rendelést. Ez szándékos rugalmasság
    (pl. korán leadott konyhai összesítő), de nincs rá védőkorlát.
13. **A „legfeljebb egy Leves ajánlat naponta" szabály csak handler-szinten él.** A Category az
    `ALaCarteItem`-en van, nem az `ALaCarteDailyOffer`-en, ezért nincs rá EF-szinten kifejezhető szűrt
    unique index (a `MenuOrderConfiguration` `WHERE [Status] = 0` mintája csak azonos táblán belüli
    oszlopra működik). A `SetDailyOfferHandler` a `UpsertOrderingPeriodHandler` mintáját követve
    `IsolationLevel.Serializable` tranzakcióban fut, tehát a handler-szintű védelem **ténylegesen
    megbízható** két egyidejű admin-hívás ellen is — csak a DB-szintű kikényszerítés hiányzik továbbra
    is (tudatos döntés, ld. fent). A `DatabaseSeeder` közvetlen EF-insertje ettől függetlenül megkerüli
    ezt az ellenőrzést — ha a Leves katalógus valaha egynél több tételt kap, a seed-logika óvatlanul két
    Leves ajánlatot is beszúrhat ugyanarra a napra.
