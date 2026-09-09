---
name: ebedrendelo-extensions
description: EbedrendeloApp specifikus konvenciók — megjelenítési helperek, valós domain-enumok, komponens-struktúra, form/validáció minta
---

# EbedrendeloApp konvenciók

Ez a skill a repóban ténylegesen meglévő mintákat dokumentálja. Domain-specifikus UI-döntés előtt
ezekből induljunk ki, ne kitalált/általános sablonból — általános MudBlazor-konvenciókért lásd a
`mudblazor-ui-first` skillt (action/button/icon szín-tábla, dialógus-, snackbar- és a11y-szabályok).

## 1. Megjelenítési helperek — kód→szöveg/ikon leképezés

**Cél:** egy enum-értékhez tartozó felirat/ikon leképezést egy helyen tartani, hogy ne 3-4 fájlban
legyen szó szerint megismételve ugyanaz a switch-kifejezés (ez ténylegesen megtörtént: a
`CategoryName`/`CategoryIcon` négy `.razor` fájlban volt duplikálva, mielőtt kiemeltük).

**Minta:** kis, fókuszált statikus osztály a `Common/<Terület>/` alatt, XML-doc kommenttel arról,
mely fájlok használják:

```csharp
// EbedrendeloApp/Common/ALaCarte/ALaCarteCategoryDisplay.cs
namespace EbedrendeloApp.Common.ALaCarte;

public static class ALaCarteCategoryDisplay
{
    public static string Name(ALaCarteCategory category) => category switch
    {
        ALaCarteCategory.Leves => "Leves",
        ALaCarteCategory.Foetel => "Főétel",
        ALaCarteCategory.Koret => "Köret",
        ALaCarteCategory.Desszert => "Desszert",
        ALaCarteCategory.Ontet => "Öntet",
        _ => category.ToString(),
    };

    public static string Icon(ALaCarteCategory category) => category switch
    {
        ALaCarteCategory.Leves => Icons.Material.Filled.SoupKitchen,
        ALaCarteCategory.Foetel => Icons.Material.Filled.DinnerDining,
        ALaCarteCategory.Koret => Icons.Material.Filled.RiceBowl,
        ALaCarteCategory.Desszert => Icons.Material.Filled.Icecream,
        ALaCarteCategory.Ontet => Icons.Material.Filled.WaterDrop,
        _ => Icons.Material.Filled.Fastfood,
    };
}
```

Ugyanez a minta már él a hibaüzenet-oldalon is: `EbedrendeloApp/Common/Results/DayUnavailableReasonText.cs`
(`ErrorCodes` → magyar szöveg, `.razor` fájlok közötti megosztásra), és a szám/pénz-megjelenítésen:
`EbedrendeloApp/Common/Formatting/HungarianNumberFormat.cs` (`int` → ezresével tagolt szöveg, lásd
`mudblazor-ui-first` "Numeric & Money Columns" szekciók) — ez utóbbi tisztán UI-rétegbeli, a domain
entitások/DTO-k mezői változatlanul `int`/`decimal` maradnak, nincs elmentett formázott szöveg.

**Nem ez a minta:** egyetlen monolit `UIConstants.cs` isten-osztály `Icons`/`Buttons`/`Inputs`/
`Colors`/`Labels` beágyazott osztályokkal. Ilyen fájl **nem létezik** a repóban, és nem is ide illő —
minden domain-fogalomhoz (kategória, hiba-kód, allergén stb.) saját, kis helper tartozik, ott, ahol a
`Common/` alatt a témája van (lásd `Common/Allergens/AllergenCatalog.cs` ugyanezért a mintáért).

### Felhasználás

```razor
@using EbedrendeloApp.Common.ALaCarte

<MudIcon Icon="@ALaCarteCategoryDisplay.Icon(context.Category)" Size="Size.Small" Color="Color.Primary" />
@ALaCarteCategoryDisplay.Name(context.Category)
```

---

## 2. Valós domain-enumok (ne találj ki másikat)

| Enum | Érték | Megjegyzés |
|---|---|---|
| `ALaCarteCategory` (`Domain/Enums`) | `Leves, Foetel, Koret, Desszert, Ontet` | **5 érték** — az `Ontet` (öntet) könnyű elfelejteni |
| `OrderStatus` (`Domain/Enums`) | `Active, Cancelled` | Csak 2 érték — nincs `Placed`/`Pending`/`Paid` |
| `CreditEntryKind` (`Domain/Entities/CreditEntry.cs`) | `CancellationCredit, CreditApplied, CreditRevoked, ManualAdjustment` | Az egyenleg egy ledger-modell (`CreditEntry`), **nem** egy "fizetési állapot" mező |

`User` entitásnak (`Domain/Entities/User.cs`) **nincs** `FullName`/`Email`/`Balance` property-je —
a mezők: `UserId, UserName, KeresztNev, VezetekNev, Rf, SzervKod, RoleId`. Ha egyenleg-UI épül,
az a `CreditEntry`/`CreditEntryKind` ledgerre aggregálva számolja ki a pillanatnyi egyenleget,
nem egy tárolt `Balance` mezőből olvassa.

---

## 3. Color Scheme

A szín a repóban mindig **szemantikus állapotot** jelöl, soha nem kategóriát — kategóriának csak
ikonja van (lásd fent), saját színe nincs.

| Jelentés | MudBlazor szín | Példa |
|---|---|---|
| Aktív / nyitott / sikeres | `Color.Success` | Aktív-toggle chip, nyitott időszak, "Megrendelve" |
| Törölt / destruktív / kizárt | `Color.Error` | Lemondott rendelés, kizárt nap, törlés-gomb |
| Függőben / figyelmeztetés | `Color.Warning` | "Nincs időszak", függő jelölőnégyzet |
| Szerkesztés-akció | `Color.Info` | Edit `MudIconButton` |
| Semleges / inaktív | `Color.Default` | Inaktív állapot, "Mégse" gomb |

Az Action/Button/Icon szín-táblát (Create/Edit/Save/Delete stb.) lásd a `mudblazor-ui-first` skillben —
az egyezik a tényleges kóddal, itt nem ismételjük meg.

---

## 4. Komponens-struktúra

Tényleges, kivétel nélkül követett elrendezés:

```
Components/
  Pages/
    ALaCarte/   AdminALaCarteItems.razor, AdminALaCarteDailyOffer.razor,
                AdminALaCarteKitchenSummary.razor, ALaCarteItemDialog.razor
    Calendar/   UserCalendar.razor, OrderingPeriods.razor, OrderingPeriodDialog.razor, ...
    Menus/      DailyMenuEditor.razor, MenuDishEditor.razor, TodayMenu.razor, ...
    Orders/     AdminOrders.razor, MyOrders.razor, PlaceOrderResultDialog.razor
  Shared/       ConfirmDialog.razor, DateNavigatorBar.razor, DecimalStepperField.razor(+.css),
                WeekdayHeaderRow.razor(+.css)
  Layout/       MainLayout.razor, NavMenu.razor, ReconnectModal.razor
```

**Fájlnév és komponens-elnevezés egyaránt PascalCase** (`DailyMenuEditor.razor`, nem
`daily-menu-editor.razor`) — kebab-case fájlnév **egyetlen** helyen sincs használva a repóban.

`Components/Shared/` a terület-független, több oldal által használt komponenseknek való;
terület-specifikus komponens/dialógus a saját `Pages/<Terület>/` alá kerül (pl. `ALaCarteItemDialog.razor`
az `ALaCarte/` alatt van, nem a `Shared/`-ben, mert csak az à la carte oldalak nyitják).

### Komponens-property konvenciók

```csharp
[Parameter]
public List<ALaCarteItemDto> Items { get; set; } = new();

[Parameter]
public EventCallback<ALaCarteItemDto> OnItemSelected { get; set; }

[Parameter]
public bool IsLoading { get; set; }
```

### Komponens ↔ szerver határ

A komponens **`IMediator`-t injektál**, és use case-t hív — soha nem `EbedrendeloDbContext`-et,
`IDbContextFactory`-t vagy repositoryt (ma nulla ilyen hivatkozás van a `Components/` alatt, tartsuk így):

```razor
@inject IMediator Mediator

@code {
    private IReadOnlyList<OrderableDayDto> days = [];

    protected override async Task OnInitializedAsync()
    {
        var result = await Mediator.Send(new GetOrderableDaysQuery(userId, periodId));
        if (result.IsSuccess)
        {
            days = result.Value!;
        }
    }
}
```

- A komponens **DTO-t köt**, nem entitást — a `Domain/Entities/` névtér `.razor`-ban nem szerepelhet.
- `Components/Pages/<Terület>/` neve tükrözi a `Features/<Terület>/` nevét (`Orders` ↔ `Orders`,
  `ALaCarte` ↔ `ALaCarte`), hogy a képernyő és a mögötte lévő use case-ek egy kereséssel meglegyenek.
- Üzleti döntés (határidő számítása, jogosultság, ár) **nem** a komponensben dől el; a `.razor` a
  `Result` kimenetét jeleníti meg. Az oldal `OnInitializedAsync`-jében lévő átirányítás csak kényelmi
  elem — a valódi kapu az `AuthorizationBehavior` a szerveren.
- Nincs `.razor.cs` code-behind; komponens-specifikus stílus `.razor.css`-be megy (CSS isolation).

---

## 5. Data Display Patterns

### MudTable — lista & szűrés (lásd `AdminALaCarteItems.razor`)

```razor
<MudTable Items="@Items" Hover="true" Dense="true" Loading="@IsLoading">
    <HeaderContent>
        <MudTh>Név</MudTh>
        <MudTh>Kategória</MudTh>
        <MudTh Style="text-align:right">Ár</MudTh>
    </HeaderContent>
    <RowTemplate>
        <MudTd DataLabel="Név">@context.Name</MudTd>
        <MudTd DataLabel="Kategória">
            <MudIcon Icon="@ALaCarteCategoryDisplay.Icon(context.Category)" Size="Size.Small" Color="Color.Primary" />
            @ALaCarteCategoryDisplay.Name(context.Category)
        </MudTd>
        <MudTd DataLabel="Ár" Style="text-align:right">@HungarianNumberFormat.Huf(context.PriceHuf)</MudTd>
    </RowTemplate>
</MudTable>
```

Pénzösszeg/szám oszlop mindig jobbra igazított és ezresével tagolt (`HungarianNumberFormat.Huf`/`.Number`,
`Common/Formatting/HungarianNumberFormat.cs`) — lásd `mudblazor-ui-first` "Numeric & Money Columns"
szekciókat, itt nem ismételjük meg a szabályt.

### Dátum-oszlopos lista alapértelmezett sorrendje

Rendelés-/napló-jellegű listánál (van dátum oszlop, valós rekordok) a lekérdezés a legújabb dátumot
adja vissza legelöl — lásd `mudblazor-ui-first` "Tables with a Date Column" szekció a szabályért és a
kivételekért. Konkrét példák a repóban:

- **Legújabb elöl (`OrderByDescending`):** `GetUserOrdersHandler.cs` (`AdminOrders.razor` rendelés-lista).
- **Kronologikus marad (kivétel, ne fordítsd meg):** `GetMyCreditLedgerHandler.cs` — a ledger ok-okozat
  sorrendje (AC 5.3.3) miatt szándékosan `OrderBy` maradt, ne cseréld `OrderByDescending`-re; naptár-/
  mátrix-nézetek (`DailyMenuEditor`, `MyOrders`, à la carte havi konyhai összesítő) és az előretekintő
  `NonOrderableDays` (jövőbeli kizárt napok, legközelebbi elöl) szintén kronologikus marad.

---

## 6. Form & validáció minta (lásd `ALaCarteItemDialog.razor`)

**Nem ez a minta:** `<MudForm Validation="@(new ItemValidator())">` vagy `<FluentValidationValidator />` —
ilyen sehol nincs bekötve a Blazor oldalon.

**A tényleges minta:**
- A validáció a **MediatR command-oldalon** fut, FluentValidation validátorral
  (`Features/<Terület>/<UseCase>/<UseCase>Validator.cs`, pl. `UpsertALaCarteItemValidator.cs`),
  amit a `ValidationBehavior` MediatR pipeline hajt végre — nem a Blazor komponensben.
- A dialógus/oldal egy egyszerű derived `bool` property-vel (`CanSave`, `CanSubmit`) tiltja a
  mentés-gombot alap-szintű kliensoldali ellenőrzésre (pl. `!string.IsNullOrWhiteSpace(name)`).
- Mentéskor a `Mediator.Send(...)` egy `Result`-ot ad vissza; hiba esetén `errorMessage` mezőbe
  kerül és `<MudAlert Severity="Severity.Error">@errorMessage</MudAlert>`-ként jelenik meg a
  dialógus tetején — nem `Snackbar`-ral (a Snackbar sikeres mentés visszajelzésére való, lásd lent).

```razor
@if (!string.IsNullOrEmpty(errorMessage))
{
    <MudAlert Severity="Severity.Error" Dense="true" Class="mb-2">@errorMessage</MudAlert>
}

<MudButton Color="Color.Success" Variant="Variant.Filled" OnClick="SaveAsync" Disabled="!CanSubmit">
    Mentés
</MudButton>

@code {
    private bool CanSave => !string.IsNullOrWhiteSpace(name) && priceHuf >= 0;
    private bool CanSubmit => CanSave && !isSaving;
    private string? errorMessage;

    private async Task SaveAsync()
    {
        isSaving = true;
        var result = await Mediator.Send(new UpsertALaCarteItemCommand(...));
        isSaving = false;

        if (result.IsSuccess) MudDialog.Close(DialogResult.Ok(result.Value));
        else errorMessage = result.ErrorMessage;
    }
}
```

---

## 7. Notifikáció & feedback (Snackbar)

Sikeres mentésnél/mutációnál `Snackbar`, hibás validációnál (lásd fent) inline `MudAlert` a dialógusban:

```csharp
Snackbar.Add("Tétel mentve.", Severity.Success);
Snackbar.Add("Tétel kivezetve.", Severity.Success);
```

Soha ne kerüljön nyers kivétel-szöveg a felhasználó elé — lásd `mudblazor-ui-first` "Error Sanitization" szekció.

---

## 8. Kiegészítendő komponens-könyvtár

A korábbi lista több tétele időközben elkészült — ez a maradék, ténylegesen hiányzó rész:

| Komponens | Célja | Állapot |
|---|---|---|
| `OrderStatusTimeline` | Rendelés életciklusának megjelenítése | Még nincs |
| Egyenleg-UI (`CreditEntry`/`CreditEntryKind` ledgerre építve) | Dolgozó egyenlegének megtekintése | Még nincs — a `User` entitáson nincs tárolt `Balance`, aggregálni kell |
| `OrderConfirmationDialog` | Rendelés megerősítése leadás előtt | Még nincs |
| `CancelOrderConfirmationDialog` | Rendelés lemondásának megerősítése | Ellenőrizendő — a lemondási flow-k már működnek (`AdminOrders.razor`, `MyOrders.razor`), de dedikált megerősítő dialógusként érdemes újra megnézni írás előtt |

---

## 9. Tesztkonvenció

A tesztprojekt **tükrözi az app szerkezetét**: `Common/`, `Components/`, `Data/Seed/`, `Features/`,
plusz `TestSupport/` a közös fake-eknek.

| Mit tesztelsz | Hogyan | Eszköz |
|---|---|---|
| Handler / use case | sima xUnit, valódi EF-fel in-memory Sqlite felett | `TestSupport/SqliteDbContextFactory.cs`, `FixedAppClock` |
| Komponens renderelése, interakció | bUnit | `TestSupport/MudBunitContext.cs` + `FakeMediator` |
| Domain-logika (munkanap, jóváírás) | sima xUnit, DB nélkül | — |
| Seed | xUnit a `Data/Seed/` alatt | `FileSqliteDbContextFactory` |

Szabályok:

- **Elnevezés:** `<OsztályNeve>Tests.cs`, benne `public class <OsztályNeve>Tests`. Ha a teszt saját
  Sqlite-kontextust birtokol, `IDisposable` és `Dispose() => dbFactory.Dispose();`.
- **Mappa:** a teszt oda kerül, ahol az osztálya van az appban —
  `Features/<Terület>/<UseCase>HandlerTests.cs`. (A `Features/Billing` alatt ma kevert a szerkezet:
  `AddManualCredit/…` almappa vs. lapos `GeneratePeriodInvoicesHandlerTests.cs`. Új teszt a **lapos**
  változatot kövesse a területen belül, kivéve ha egy use case-hez több tesztfájl tartozik.)
- **Idő:** `FixedAppClock`, fix hétköznapra eső referencia-dátummal, kommentben megjelölve, melyik nap
  az (pl. `// 2026-08-17 is a Monday`). Soha ne `DateTime.Now`-tól függjön a teszt.
- **Komponens-teszt ne menjen adatbázisig:** a `FakeMediator` adja vissza a DTO-t, a teszt a
  renderelést és az interakciót ellenőrzi.
- Minden új komponenshez bUnit teszt, minden új handlerhez handler-teszt — a logika-tesztek bUnit
  nélkül, ezredmásodperc alatt futnak, a bUnit csak a megjelenítést ellenőrzi.
