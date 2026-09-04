# Nyitott Teendők / Backlog

> **Ez a dokumentum egyetlen mérvadó példánya.** Más helyen (home `.claude/plans/`, `docs/`) ne
> keletkezzen belőle másolat.
>
> Ide kerülnek azok az észrevételek, hibák és finomítási ötletek, amik fejlesztés közben merülnek fel,
> de nem blokkolják az aktuális user story-t — nem kell rögtön megoldani, csak ne vesszen el.
> A user story-kon való végigmenet után érdemes visszanézni és rendszerezni/priorizálni.

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
- [ ] A napi menü szerkesztésénél lévő címsor (title, ikon, subtitle, jobb oldali extra tartalom
      pl. select) legyen kiemelve önálló, újrafelhasználható komponensbe, és vezessük át az összes
      oldalra, ahol hasonló fejléc kell (fragment/RenderFragment a variábilis résznek).
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

---

*Új tétel felvételekor elég egy rövid, egy-két mondatos leírás — a részletes elfogadási kritériumok
majd a tényleges implementáció előtt kerülnek elő, a `02-user-stories.md` mintájára, ha a tétel
önálló story-vá nő.*
