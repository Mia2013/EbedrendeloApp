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
      **Kivéve a menürendelést**: az AC 3.1.6/9.2.2 szerint bárki rendelhet bárki nevében, ezt először
      tévesen adminhoz kötöttem. Javítva: a rendelés/lemondás/naptár az `IAuditedOnBehalfOf` jelölőt
      viseli (nyitva marad, a védelem az audit + a címzett azonosítása), a pénzügyi lekérdezések
      viszont `IActsOnBehalfOf`-fal admin-jogot kívánnak idegen felhasználóra. A
      `UseCaseAuthorizationCoverageTests` külön teszttel őrzi, hogy ez a négy use case nyitva maradjon.
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
- [ ] **`UserNotification`: 8 írási hely, 0 olvasási.** Minden értesítés a táblába megy, de nincs se
      query, se UI — ez az Epic 8. Amíg nincs kész, a dolgozó soha nem tudja meg, hogy lemondták vagy
      átvezették a rendelését.
- [ ] **`AppSetting` szerkeszthetetlen** — adagár és a három határidő seedből jön, nincs admin felület,
      az `UpdatedByUserId`/`UpdatedAtUtc` halott mező. Az érték ma csak SQL-ből módosítható.
- [ ] **À la carte fizetés rögzítése** — a dolgozó aznap fizeti (AC 7.1.2), de a rendszer csak a
      rendelést tárolja, a fizetést nem; nincs pénztár/kassza-modul, így az à la carte pénzügyileg nem
      zárható le. Ha kell: fizetés-állapot az `ALaCarteOrder`-en + napi kassza-riport.

### Konvenció-driftek
- [ ] 6 command-nak nincs FluentValidation validátora, a validáció inline `if` a handlerben
      (`CloseDay`, `ReopenDay`, `RemoveExcludedDay`, `SetALaCarteItemActive`, `RemoveDailyOffer`,
      `CancelALaCarteOrderLine`).
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

---

*Új tétel felvételekor elég egy rövid, egy-két mondatos leírás — a részletes elfogadási kritériumok
majd a tényleges implementáció előtt kerülnek elő, a `02-user-stories.md` mintájára, ha a tétel
önálló story-vá nő.*
