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
- [ ] A `MudAutocomplete` (leves/főétel név) egysoros, nincs autogrow/multiline támogatása —
      a napi menü szerkesztő 3-oszlopos elrendezésében a hosszabb ételnevek levágódnak.
      Megoldás még nyitott (tooltip a teljes névvel, és/vagy szélesebb elrendezés) — lásd az
      alábbi pontot, mert összefügg vele.
- [ ] Az ételekhez (leves/főétel) az eddigi Név + Allergének mellé jön egy tápérték-adatblokk is:
      energia (kcal), zsír, telített zsír, szénhidrát, cukor, fehérje, só (pl. minta: „Mentás
      zöldborsóleves — Allergének: 1,8,11, En: 108, Zs: 1.8, T.Zs: 0.4, Szh: 16.0, Cuk: 2.1,
      Feh: 6.0, Só: 0.14"). Ezt meg kell jeleníteni **mindenhol**, ahol jelenleg az étel neve +
      allergénjei látszik: napi menü szerkesztő (admin), mai menü / heti menü nézet (dolgozói
      oldal), és feltehetően a `MenuDishSuggestionsDto` / autocomplete javaslatokban is. Nyitott
      kérdések, amiket implementáció előtt tisztázni kell:
      - hol tárolódik ez az adat (bővül-e a leves/főétel katalógus entitása, vagy külön tábla),
      - admin szerkeszthető-e mezőnként, vagy fix forrásból (pl. import) jön,
      - megjelenítési forma (kompakt chip-sor, kibontható részlet, tooltip) — ez már a fenti
        `MudAutocomplete`-szélesség kérdéssel is összefügg, együtt érdemes megtervezni.

---

*Új tétel felvételekor elég egy rövid, egy-két mondatos leírás — a részletes elfogadási kritériumok
majd a tényleges implementáció előtt kerülnek elő, a `02-user-stories.md` mintájára, ha a tétel
önálló story-vá nő.*
