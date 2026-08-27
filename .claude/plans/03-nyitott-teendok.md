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

## Rendelés (Epic 3)

- [ ] US-2.1 AC 2.1.3 (menü ár snapshotolása rendeléskor) jelenleg nem valósítható meg és
      nem tesztelhető, mert Epic 3 (Menü Előrendelés és Lemondás) még nincs megírva —
      nincs `PlacePeriodOrderCommand`/`Features/Orders`. Az `AppSetting.MenuPortionHuf` és a
      `MenuOrder.PriceHuf` mező már megvan az adatmodellben, csak az író use case hiányzik.

## UI / komponensek

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

---

*Új tétel felvételekor elég egy rövid, egy-két mondatos leírás — a részletes elfogadási kritériumok
majd a tényleges implementáció előtt kerülnek elő, a `02-user-stories.md` mintájára, ha a tétel
önálló story-vá nő.*
