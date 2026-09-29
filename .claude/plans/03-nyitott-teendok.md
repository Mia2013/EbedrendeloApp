# Nyitott Teendők / Backlog

> **Ez a dokumentum egyetlen mérvadó példánya.** Más helyen (home `.claude/plans/`, `docs/`) ne
> keletkezzen belőle másolat.
>
> Ide kerülnek azok az észrevételek, hibák és finomítási ötletek, amik fejlesztés közben merülnek fel,
> de nem blokkolják az aktuális user story-t — nem kell rögtön megoldani, csak ne vesszen el.
> A user story-kon való végigmenet után érdemes visszanézni és rendszerezni/priorizálni.

---

## Epic 1–7 átvilágítás (2026-09-08) — még nyitott tételek

A teljes átvilágítás megállapításai. Elkészült: **Fázis 1** (számlázási modell + jóváírás-szabály),
**Fázis 2** (szerveroldali jogosultság, `ErrorBoundary`, naplózás). Az alábbiak maradtak.

### Biztonság — elkészült (Fázis 2)
- [x] **Szerveroldali jogosultság-ellenőrzés** — `AuthorizationBehavior` MediatR pipeline behavior
      (`ValidationBehavior` ELŐTT fut), `IRequireAdmin` és `IActsOnBehalfOf` jelölőkkel; 32 admin és
      10 saját-adat use case megjelölve. A `UseCaseAuthorizationCoverageTests` architektúra-teszt
      elbukik, ha új use case jelöletlenül csúszik be, így a védelem nem tud csendben lyukassá válni.
- [x] **Idegen nevében végzett műveletek** — a `TargetUserId` ellenőrzött, idegen id csak adminnak.
      **Kivéve a rendelés leadását**: az AC 3.1.6/9.2.2 szerint bárki rendelhet bárki nevében, ezt először
      tévesen adminhoz kötöttem. Javítva, majd a második körben szűkítve: **csak** a
      `PlacePeriodOrderCommand` és a `GetOrderableDaysQuery` viseli az `IAuditedOnBehalfOf` jelölőt
      (nyitva marad, a védelem az audit + a címzett azonosítása). A lemondás
      (`CancelMenuOrdersCommand`, AC 3.2.8), a rendeléstörténet (`GetMyPeriodOrderQuery`, AC 3.1.9) és a
      pénzügyi lekérdezések `IActsOnBehalfOf`-fal admin-jogot kívánnak idegen felhasználóra. A
      `UseCaseAuthorizationCoverageTests` két teszttel őrzi mindkét oldalt: hogy ez a **két** use case
      nyitva maradjon, és hogy a másik kettő ne nyíljon ki.
- [x] **Paraméterértékhez kötött jog** — a `GetPeriodMenuQuery` maga nyitott (a dolgozói naptár is
      hívja), de az `IncludeUnpublished` kapcsoló admin-jog (AC 2.5.2). Jelölővel ez nem fejezhető ki,
      mert nem a kérés, hanem egy paraméterérték igényel jogot, ezért a handler őrzi. A párja, a
      `GetDailyMenuQuery` egészében `IRequireAdmin`, mert azt csak admin felület hívja.
- [x] **`IDevUserSwitcher` kivezetése** — a `UserCalendar` és az `AdminOrders` a rendes
      `GetUsersQuery`-t használja; a `Home` dev-kártyája `IsDevelopment()` mögé került.
- [x] **Naplózás** — a két behavior és a pénzmozgató handlerek (`GeneratePeriodInvoices`,
      `MarkInvoicePaid`, `AddManualCredit`, `CancelMenuOrders`) `ILogger`-t kaptak.

### Hibatűrés
- [x] **`AppErrorBoundary`** a `MainLayout`-ban — a `ForbiddenException` és a `ValidationException`
      barátságos kártyát kap (a váratlan kivételek üzenete elrejtve), a natív sárga sáv és a ledőlt
      circuit helyett. Útvonalra kulcsolva, hogy a hiba ne ragadjon be a következő oldalon.
- [ ] **16 helyen `result.Value!`** ellenőrzés nélkül (`AdminInvoices`, `MyInvoices`, `MyBalance`,
      `AdminBalances`, `UserCalendar`, `MyOrders`, `AdminOrders`, `DailyMenuEditor`,
      `ManualCreditDialog`) — sikertelen `Result` esetén NRE. Az `ErrorBoundary` már elkapja, de a
      helyes megoldás a `Result` ellenőrzése (közös állapot-komponenssel, lásd lent).

### UI egységesítés — nagyrészt elkészült (Fázis 3)
- [x] **`PageHeader`** — az ikon+cím+leírás fejléc 13 oldalról egy komponensbe (`Description`,
      `Filters`, `Actions`, `ChildContent` slotokkal). A régi backlog-tétel („UI / komponensek"
      szekció) ezzel lezárva.
- [x] **`WeekGrid`** — a négyszer lemásolt hétfő–péntek rács (`order-`, `menu-`, `admin-orders-`,
      `my-orders-calendar`) és a négy `BuildWeeks()` egy komponensbe. A cellák `MudPaper`-ek, a
      szaggatott üres cella MudBlazor border-utility osztályokból. Az oszlopszám/térköz `app.css`-beli
      CSS-változóban, közösen a `WeekdayHeaderRow`-val, hogy ne csúszhassanak el.
- [x] **`PeriodSelector`** — az öt oldalon újraírt időszak-választó egy komponensbe.
- [x] **`PageState`** — közös betöltés / üres / hiba állapot.
- [x] **Kézi CSS: nettó −165 sor**, két `.razor.css` teljesen törölve. Ami maradt: a rács geometriája
      (MudGrid 12 oszlopos rendszere nem tudja), a beküldő sáv `position: sticky`-je, és a korábban is
      jogos kivételek (`ReconnectModal`, `#blazor-error-ui`, `DecimalStepperField`).
      **Tanulság:** a `MudPaper` gyerekkomponens, ezért a CSS-izoláció `b-xxx` attribútuma nem kerül rá
      — a rá vonatkozó szabályokhoz `::deep` kell, különben némán nem érvényesülnek (böngészőben,
      computed style-lal derült ki).

### UI — még nyitott
- [ ] **`result.Value!` a maradék ~12 helyen** (`AdminBalances`, `MyBalance`, `UserCalendar`,
      `AdminOrders`, `MyOrders`, `DailyMenuEditor`, `ManualCreditDialog`) — az `AdminInvoices` és a
      `MyInvoices` már `PageState`-tel, ellenőrzött `Result`-tal megy, a többit is át kell vezetni.
- [ ] **A `/` kezdőlap tartalma gyakorlatilag egy dev eszköz** — egy gomb + a felhasználóváltó kártya
      (ez utóbbi már csak fejlesztői környezetben). A dolgozónak nincs áttekintője (mai menü, egyenleg,
      fizetetlen számla, gyorslinkek).
- [ ] **A sötét paletta halott** — `AppTheme.PaletteDark` definiálva, de a `MudThemeProvider` nincs
      `IsDarkMode`-hoz kötve és nincs kapcsoló. Vagy kössük be, vagy töröljük.
- [ ] **Visszajelzés-konvenció rögzítése** a `CLAUDE.md`-be: mutáció eredménye → Snackbar; oldal-szintű
      állapot → inline `MudAlert`; dialóguson belüli szerverhiba → dialóguson belüli alert. Ma keverednek.
- [ ] Inline `Style="…"` maradékok átnézése (sűrűsödve `AdminInvoices`, `AdminALaCarteDailyOffer`
      körül), szemben a „MudBlazor komponens/utility a kézi CSS helyett" elvvel.

### Hiányzó / halott funkciók
- [x] **`UserNotification`: 8 írási hely, 0 olvasási** — megoldva az Epic 8-cal: `/ertesiteseim` oldal,
      menüpont- és csengő-számláló, és az AC 8.1.3 leadói értesítés minden rendelés-eseményre.
- [ ] **`AppSetting` szerkeszthetetlen** — adagár és a három határidő seedből jön, nincs admin felület,
      az `UpdatedByUserId`/`UpdatedAtUtc` halott mező. Az érték ma csak SQL-ből módosítható.
- [ ] **À la carte fizetés rögzítése** — a dolgozó aznap fizeti (AC 7.1.2), de a rendszer csak a
      rendelést tárolja, a fizetést nem; nincs pénztár/kassza-modul, így az à la carte pénzügyileg nem
      zárható le. Ha kell: fizetés-állapot az `ALaCarteOrder`-en + napi kassza-riport.

### Konvenció-driftek

> **A javítás sorrendje: előbb a szabály, aztán a kód.** Ha review-n drift derül ki, először a
> `CLAUDE.md` vagy a megfelelő skill (`ebedrendelo-usecase`, `ebedrendelo-extensions`,
> `mudblazor-ui-first`) mondja ki a szabályt — így a következő generálás már nem termeli újra —, és a
> kódjavítás ide kerül tételként. A gépileg ellenőrizhető rész a gyökér `.editorconfig`-ban van
> (`EnforceCodeStyleInBuild`, warning szinten).

- [ ] 6 command-nak nincs FluentValidation validátora, a validáció inline `if` a handlerben
      (`CloseDay`, `ReopenDay`, `RemoveExcludedDay`, `SetALaCarteItemActive`, `RemoveDailyOffer`,
      `CancelALaCarteOrderLine`).
- [ ] **Architektúra-tesztek** a `EbedrendeloApp.Tests`-be, a működő `UseCaseAuthorizationCoverageTests`
      mintájára: (a) `Domain.Entities` / `EbedrendeloDbContext` nem hivatkozható a `Components/` alól;
      (b) minden `IRequest` command-hoz tartozik validátor — ez utóbbi ma a fenti 6 helyen bukna, ezért
      csak allowlisttel, vagy a driftek javítása után vezethető be.
- [ ] **`EbedrendeloApp/Migrations/`** üres, verziókövetetlen mappa a lemezen (a valódi hely a
      `Data/Migrations/`, 11 migrációval). Csak lokális maradék, törölhető — de érdemes ellenőrizni,
      hogy a `dotnet ef migrations add` tényleg a `Data/Migrations/`-ba generál-e.
- [ ] **`Program.cs` blokkos namespace-t használ** — a build egyetlen figyelmeztetése az
      `.editorconfig` bekapcsolása után (`IDE0161`). Mechanikus javítás file-scoped namespace-re, de
      a `RegisterServicesFromAssemblyContaining(typeof(Program))` miatt build+teszt kell utána.
- [ ] `IDE0005` (felesleges using) fordításkor nincs bekapcsolva, mert `GenerateDocumentationFile`-t
      igényelne, az pedig több száz `CS1591`-et hozna. Ha kell, `GenerateDocumentationFile=true` +
      `NoWarn=CS1591` a `Directory.Build.props`-ban.
- [ ] A `Features/Billing` tesztmappa-konvenció kevert: `AddManualCredit/…` (use case almappa) vs.
      `GeneratePeriodInvoicesHandlerTests.cs` (lapos).
- [ ] A kommentnyelv hol magyar, hol angol, néha egy fájlon belül.

---

## Rendelési időszak (Epic 1)

- [ ] Új időszak felvételekor a kezdő dátum alapértelmezetten a legutolsó (meglévő) időszak
      végdátuma + 1 nap legyen, ne kelljen manuálisan kikeresni.
- [ ] Az időszak dialógban, ha a felhasználó kiválasztja a kezdő dátumot, a záró dátum
      datepickerén az azt megelőző napok legyenek letiltva (csak a kezdő dátumnál későbbi
      választható).
- [ ] Rendelési időszak jelenleg nem törölhető. Ha az admin rosszul vette fel, és még nincs
      hozzá rendelés, engedjük a törlést (ha már van rendelés hozzá, maradjon tiltva).

## UI / komponensek

- [ ] Design-referencia: https://happyetterem.hu/fooldal — a designja nagyon tetszett, érdemes
      majd megnézni, mit lehetne belőle átvenni.
- [x] A napi menü szerkesztésénél lévő címsor (title, ikon, subtitle, jobb oldali extra tartalom
      pl. select) legyen kiemelve önálló, újrafelhasználható komponensbe, és vezessük át az összes
      oldalra, ahol hasonló fejléc kell (fragment/RenderFragment a variábilis résznek) — megoldva a
      Fázis 3-ban: `Components/Shared/PageHeader.razor`, `Description`/`Filters`/`Actions`/
      `ChildContent` slotokkal, mind a 13 oldalon átvezetve.
- [x] A `MudAutocomplete` (leves/főétel név) a napi menü szerkesztő 3-oszlopos elrendezésében a
      hosszabb ételnevek miatt levágódott — megoldva a dialógus szélesítésével
      (`DialogOptions { MaxWidth = MaxWidth.ExtraLarge, FullWidth = true }`,
      `DailyMenuEditor.razor` `EditDayAsync`) és a `MudGrid` térköz növelésével.
- [x] Az ételekhez (leves/főétel) a Név + Allergének mellé tápérték-adatblokk jelenik meg a napi
      menü szerkesztő dialógusban — kiválasztás után egy kis kártyában (`DishDetailsCard.razor`),
      allergén chip-sorral és a 7 tápérték-mezővel. **Még nincs** átvezetve a mai menü / heti menü
      nézetre (dolgozói oldal) — ott továbbra is a régi kompakt egysoros formátum
      (`MenuVariantNutritionFormat.Format`) fut; ha ott is kártyás megjelenítés kell, a
      `DishDetailsCard` innen újrafelhasználható.
- [ ] Admin felület a leves/főétel katalógus (`MenuDish`) önálló kezelésére: létrehozás,
      szerkesztés, törlés/deaktiválás. A napi menü szerkesztő dialógusból (`EditDailyMenuDialog`)
      szándékosan eltávolításra került az inline "Hozzáad" és a szerkesztés-ceruza — az a dialógus
      mostantól csak a már meglévő katalógusból választ. A hozzá tartozó UI építőelemek már készen
      állnak és újrafelhasználhatók: `MenuDishEditor.razor` (a mezőkészlet) és `AddMenuDishDialog.razor`
      (dialógus-keret köré rá) — ez utóbbi jelenleg sehonnan nincs megnyitva, csak tesztelve van
      (`AddMenuDishDialogTests.cs`). Az admin felületnek valószínűleg egy listázó nézetre is
      szüksége lesz (jelenleg nincs "összes leves/főétel" lekérdezés, csak a `GetMenuDishSuggestionsQuery`,
      ami a napi menü szerkesztőhöz készült).

## Napi menü / étel-katalógus (Epic 2) — code review során talált, még nyitott kockázatok

- [ ] `UpsertDailyMenuValidator` a variánskódok egyediségét `StringComparer.Ordinal`-lal (kis-nagybetű
      érzékenyen) ellenőrzi, miközben a `MenuVariant` DB-oldali unique indexe (`DailyMenuId`, `Code`)
      SQL Serveren alapértelmezetten kis-nagybetű független collationt használ. Emiatt pl. "A" és "a"
      kódok átcsúszhatnak a validáción, majd `SaveChangesAsync`-nél nyers `DbUpdateException`
      (unique constraint violation) száll fel egy barátságos `Result.Failure` helyett. Javítás: a
      validátor is legyen kis-nagybetű független (`StringComparer.OrdinalIgnoreCase`), hogy a hiba még
      a mentés előtt, szép hibaüzenettel bukjon el.
- [ ] `GetTodayMenuForUserHandler.cs` egyik (fallback) lekérdezése — a felhasználó már leadott
      rendeléséhez tartozó `MenuVariant` keresése — nem szűr `RemovedAtUtc == null`-ra, és `FirstAsync`-et
      használ `FirstOrDefaultAsync` helyett (eltérően a fájl és a feature többi lekérdezésétől). Ma nem
      hívható elő éles hibaként, mert minden variáns-törlési útvonal (`DeleteMenuVariantHandler`,
      `UpsertDailyMenuHandler`, `DeleteDailyMenuHandler`) előbb átvezeti/lemondja az érintett aktív
      rendeléseket a `MenuReassignmentService`-en keresztül, szóval aktív rendelés ma nem mutathat
      törölt variánsra — de ha ez az invariáns egy jövőbeli módosítással megszűnik, ez a sor
      kezeletlen `InvalidOperationException`-t dobna a "mai menü" oldalon. Érdemes a többi
      lekérdezéshez hasonlóan `RemovedAtUtc == null` + `FirstOrDefaultAsync`-re javítani, kis
      védelemként.

## Rendelés (Epic 3)

- [x] `GetOrderableDaysQuery` mostantól minden sorban visszaadja az `AppSetting.MenuPortionHuf`
      adagárat is (`OrderableDayDto.MenuPortionHuf`, régi hívóknak `= 0` default). A
      `UserCalendar.razor` checkboxos napi kiválasztásánál ebből épül fel a variánsonkénti
      darab/ár összesítő táblázat (+ végösszeg) a "Rendelés leadása" gomb fölött, beküldés előtt —
      korábban csak a kiválasztott napok száma látszott, ár nélkül.
- [x] Köteges lemondás UI — megoldva. A `UserCalendar.razor` (`/naptar`) a rendelés-leadáshoz
      hasonló "jelölj ki többet, majd küldd el egyben" mintát követi: minden lemondható napon egy
      törlés-ikon jelöli be a napot a `pendingCancellations` halmazba (kattintásra checkboxként
      viselkedik, vissza is vonható), majd a "Lemondás megerősítése (N nap)" gomb egyetlen
      `CancelMenuOrdersCommand` hívásban küldi be az összes kijelölt dátumot. A leírásban említett,
      egyelemű listát küldő `CancelMenuOrderDialog.razor` időközben meg is szűnt — nincs már
      egyesével megerősítendő "Lemondás" gomb naptár-cellánként.

## Egyenleg-kezelés (Epic 5)

- [x] `AdminBalances.razor` sorain a névhez egy lenyíló mutatja az adott dolgozó egyenleg-történetét —
      megoldva. Soronként egy expand-ikon (`MudTable` `ChildRowContent`) nyitja/csukja, a
      `MyBalance.razor`-ban már meglévő `GetMyCreditLedgerQuery`-t hívja `UserId` paraméterrel,
      felhasználónként gyorsítótárazva (csak az első nyitáskor kérdez le). Új jóváírás rögzítése után
      a gyorsítótár és a nyitott sorok törlődnek, hogy egy esetleg nyitva hagyott előzmény ne
      maradjon elavult (a dialógus a `CreditEntry` id-ját adja vissza, nem a célfelhasználót, ezért
      szelektív frissítés helyett a teljes gyorsítótár ürül).
- [ ] `ManualCreditDialog.razor` felhasználó-választója (`MudAutocomplete` + `SearchUsersAsync`,
      névben/igazgatóságban/osztályban keres) nem elég jó — pontosítandó, mi hiányzik belőle
      (esetleg gyorsabb/pontosabb találati sorrend, vagy más keresési szempont).
- [ ] `ManualCreditDialog.razor` `amountHuf` mezője jelenleg `1`-re inicializálódik — legyen
      alapértelmezetten az aktuális napi menü ára (`AppSetting.MenuPortionHuf`, lekérdezhető, lásd
      `GetOrderableDaysHandler`/`PlacePeriodOrderHandler` hasonló felhasználását), ne kelljen minden
      alkalommal kézzel beírni a szokásos 1400 Ft-ot.

## Konyhai összesítés és napzárás (Epic 6) — code review során talált, nem blokkoló észrevételek

- [ ] `CloseDayCommand`/`ReopenDayCommand`-hoz nincs önálló FluentValidation validátor osztály — a
      validáció (pl. "már le van zárva" / "nincs érvényben lévő zárás") inline `if` a
      `CloseDayHandler`/`ReopenDayHandler`-ben van, eltérően a többi feature konvenciójától.
      Funkcionálisan helyes, csak konzisztencia kérdés.
- [x] `KitchenSummary.razor` (és a többi admin oldal) kliens-oldali redirectje — megoldva a Fázis 2-ben:
      a valódi kapu az `AuthorizationBehavior` a szerveren, az oldal `OnInitializedAsync`-jében lévő
      átirányítás onnantól csak kényelmi elem (ne a hibakártyát lássa, aki rossz linkre téved).
      Route-szintű `[Authorize]` az Epic 9 valódi hitelesítésével jön.

## Code review (2026-09-08, max) — javítva

- [x] **`PageHeader` öt oldalon nem renderelt.** A `Components/_Imports.razor` nem importálta az
      `EbedrendeloApp.Components.Shared` névteret, így öt oldalon (`AdminALaCarteItems`,
      `AdminBalances`, `MyBalance`, `NonOrderableDays`, `OrderingPeriods`) a Razor ismeretlen
      HTML-elemként renderelte a `<PageHeader>`-t: eltűnt a címsor, az ikon és a kártya. Nem fordítási
      hiba volt, csak 14 `RZ10012` warning — és az inkrementális build ezeket nem mutatta újra, ezért
      „részleges build zajának" néztem. Javítva egy sorral az `_Imports`-ban, plusz `PageHeaderTests`,
      ami a renderelt `<h4>`-re állít.
- [x] **A seed jóváírást gyártott ki nem számlázott napokra.** A `SeedMenuOrdersAsync` feltétel nélkül
      írt `CancellationCredit`-et minden lemondott seed-rendelésre, holott a Fázis 1 óta jóváírás csak
      kiszámlázott napért jár — az első éles számlagenerálás ezekkel a fantom-jóváírásokkal csökkentette
      volna a valódi számlákat. Javítva: a seed a lezárt előző időszakra kiállítja a számlákat és
      rábélyegzi a `PeriodInvoiceId`-t (így a demó ledger és a Számlák képernyő is kap adatot), és
      jóváírás csak a kiszámlázott napokért keletkezik.
- [x] **`GenerateInvoicesDialog` szövege ellentmondott a delta-számlázásnak.** Azt ígérte, hogy „akinek
      már van számlája, kimarad — biztonságosan újrafuttatható", miközben a handler kiegészítő számlát
      állít ki. Átírva, és kikerült belőle az à la carte mondat is, ami a Fázis 1 óta halott szöveg.
- [x] **`GetPeriodMenuQuery` nem volt védve.** Lásd fent, a paraméterértékhez kötött jognál.
- [x] **A jóváírás-sáv a kolléga naptárán is látszott** — a *saját* egyenlegedről szólt, miközben minden
      pipa a kollégát terheli. Elrejtve idegen naptárban.
- [x] **`Cancellable` figyelmen kívül hagyta az idegen nézetet.** A `GetOrderableDaysQuery` — amit a `01`
      §6 „a felület egyetlen igazságforrása"-ként ír le — `true`-t adott olyan napra, amit a hívó nem
      mondhat le; a tiltás csak a razorban élt. Javítva a handlerben, az ok viszont `AlreadyOrdered`
      marad (a nap rendben van, csak a hívónak nincs joga).
- [x] **Több időszakra leadáskor elveszett a részleges siker.** Ha a második szakasz elbukott, az első
      (már commitolt) eredménye eldobódott, és az egész naptár helyére hibasáv került. Javítva: a
      beküldött napok kikerülnek a kijelölésből, a meghiúsult szakasz pipái maradnak, és az összesítő
      dialógus megmutatja, mi ment át.
- [x] **Gépidő a szerver órája helyett.** A `UserCalendar` és a `WeekGrid` `DateTime.Today`-ből számolt,
      miközben a szerver `IAppClock`-kal (Europe/Budapest) szeleteli ugyanazt a naptárat — konténerben
      éjfél után egy nap eltérés. Mindkettő az `IAppClock`-ra váltott.
- [x] **A dialógusok kívül estek a hibahatáron.** A `MudDialogProvider` a `MudLayout` testvére, az
      `AppErrorBoundary` viszont csak a `@Body`-t fogta — miközben a pénzmozgató műveletek jórészt
      dialógusból indulnak, és a behaviorök szándékosan kivételt dobnak. Saját hibahatárt kapott.

## Code review (2026-09-08, max) — még nyitott

- [ ] **Az audit-mezők hívó-adta értékek.** A `PlacedByUserId` / `CancelledByUserId` /
      `GeneratedByUserId` / `MarkedPaidByUserId` / `PerformedByUserId` úgy kerül a naplóba, ahogy a hívó
      küldte — az `AuthorizationBehavior` csak a `TargetUserId`-t nézi. Márpedig az AC 9.2.2 szerint épp
      ez az audit a más nevében rendelés egyetlen védelme. A behavior már injektálja az `ICurrentUser`-t,
      tehát egy helyen rá lehetne bélyegezni. Epic 9-cel együtt érdemes.
- [ ] **`IAuditedOnBehalfOf` futásidőben no-op**, de a lefedettségi tesztet kielégíti — egy új, érzékeny
      use case-re rátéve zölden átmegy úgy, hogy semmi nem védi. Az `IntentionallyUnrestricted` lista
      legalább teszt-fájl szerkesztést kíván. Érdemes a jelölőt is a behaviorban „látni" (legalább
      naplózni).
- [ ] **A kolléga-azonosítás nincs korlátozva.** A `ResolveColleagueQuery` a teljes admin-oldali
      `UserOptionDto`-t adja vissza (belépőnév, céges `UserId`, szerepkör), miközben a felület csak a
      nevet és az egységet mutatja — és nincs se rate limit, se lockout. Egy szűk
      `ResolvedColleagueDto(Id, DisplayName)` és egy próbálkozás-számláló zárná be.
- [ ] **A számlagenerálás nem kezeli a párhuzamos futást.** A `SaveChangesAsync` nincs `try/catch`-ben,
      így az egyediségi index (amit a `PeriodInvoiceConfiguration` épp erre hivatkozva véd) nyers
      `DbUpdateException`-ként vagy deadlockként jön vissza. A `PlacePeriodOrderHandler` már pontosan
      ezt kezeli — onnan másolható a minta.
- [ ] **A seed minden induláskor ír.** A `SeedUsersAsync` szándékosan nem lép ki, ha a tábla nem üres
      (különben a meglévő fejlesztői DB nem kapná meg a katalógust), viszont a `Program.cs` nem köti
      `IsDevelopment()`-hez a `SeedAsync`-et. Éles használat előtt kapuzni kell.
- [ ] **Maradék `DateTime.Today` a komponensekben.** Hat helyen (`AdminALaCarteDailyOffer`,
      `AdminALaCarteKitchenSummary`, `KitchenSummary`, `DailyMenuEditor`, `AdminOrders`, `MyOrders`)
      még a gép helyi ideje a kiindulópont. Ezek ma megjelenítési alapértékek, de ugyanaz a csapda.
- [ ] **Nincs bUnit teszt a `PageState`, `WeekGrid` és `PeriodSelector` komponensekre** (a `PageHeader`
      már kapott). A `WeekGrid` viszi az egyetlen valódi logikát (hétfő-igazítás, üres vezető hét
      levágása) négy naptár-oldal alatt.
- [ ] **`AdminInvoices` lapozás nélkül** tölti be az összes számlát; `ColleaguePicker.SearchAsync`
      minden leütésre újra lekéri a teljes névsort (a `ManualCreditDialog` egyszer cache-eli);
      a `PageState.razor.css` kézi flex-blokkja `MudStack`-kel kiváltható.
- [ ] **A migráció `Down()` felében elbukik**: az `FK_CreditEntries_PeriodInvoices` `Restrict`, ezért a
      `DELETE FROM PeriodInvoices WHERE SequenceNumber > 1` nem fut le.

## Értesítések (Epic 8) — code review (2026-09-29), nem blokkoló

- [ ] **Szóköz görgeti az oldalt** az `/ertesiteseim` olvasatlan során (`role="button"`): a jelölés
      megtörténik, de az oldal is ugrik. `:preventDefault` natív `div`-re kell (RZ10010), és csak a
      Szóközre — a Tab maradjon.
- [ ] **A számlálót kétszer kérdezzük** az oldalon: a `MyNotifications` saját `GetNotificationCountsQuery`-t
      futtat, miközben a `NotificationBadgeState` is. Ha a badge-állapot a `Total`-t is tartaná, az oldal
      abból olvashatna.
- [ ] **Kézi inline stílus** a `MyNotifications.razor`-ban (`opacity:.65`, `primary-hover` háttér, a pötty
      `font-size`-a) — MudBlazor megoldásra cserélendő (pl. `MudBadge Dot`, `Size`).
- [ ] **A szűrő-chip száma és a lista eltérhet**: „Olvasatlan (35)" mellett a lista 20 sort mutat
      (`GetMyNotificationsQuery.Limit`), jelzés nélkül. Legalább egy „további N régebbi" sor kellene.
- Szándékos, nem hiba: a saját műveletéről a **tulajdonos** is kap értesítést (pl. saját lemondásnál a
  jóváírásról), a leadó viszont nem, ha ő végezte a műveletet (01 §6 Notifications).

---

*Új tétel felvételekor elég egy rövid, egy-két mondatos leírás — a részletes elfogadási kritériumok
majd a tényleges implementáció előtt kerülnek elő, a `02-user-stories.md` mintájára, ha a tétel
önálló story-vá nő.*
