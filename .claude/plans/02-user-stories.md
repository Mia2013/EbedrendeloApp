# Ebédrendelő Alkalmazás — User Storyk és Elfogadási Kritériumok

> **Ez a dokumentum egyetlen mérvadó példánya.** Más helyen (home `.claude/plans/`, `docs/`) ne
> keletkezzen belőle másolat.
>
> **Forrás:** `01-szerver-architektura.md` — Ebédrendelő — szerver oldali architektúra és implementációs terv  
> **Szerepkörök:**  
> - `[U]` = Felhasználó / Dolgozó  
> - `[A]` = Adminisztrátor / Konyhavezető  
>
> A dokumentum végén lévő **lefedettségi mátrix** köti össze a story-kat az architektúra 6. fejezetének
> use case listájával. Ha a use case készlet változik, a mátrixot is frissíteni kell.

---

## Tartalomjegyzék
1. [Epic 1: Naptár és Időszakkezelés](#epic-1-naptár-és-időszakkezelés)
2. [Epic 2: Étlap- és Variánskezelés](#epic-2-étlap--és-variánskezelés)
3. [Epic 3: Menü Előrendelés és Lemondás](#epic-3-menü-előrendelés-és-lemondás)
4. [Epic 4: Aznapi A La Carte Rendelés és Készletfoglalás](#epic-4-aznapi-a-la-carte-rendelés-és-készletfoglalás)
5. [Epic 5: Jóváírás-könyvelés (Ledger) és Egyenlegkezelés](#epic-5-jóváírás-könyvelés-ledger-és-egyenlegkezelés)
6. [Epic 6: Konyhai Összesítés és Napzárás](#epic-6-konyhai-összesítés-és-napzárás)
7. [Epic 7: Elszámolás és Számlázás](#epic-7-elszámolás-és-számlázás)
8. [Epic 8: Értesítések és Tájékoztatás](#epic-8-értesítések-és-tájékoztatás)
9. [Epic 9: Felhasználókezelés és Hozzáférés](#epic-9-felhasználókezelés-és-hozzáférés)
10. [Epic 10: Kereszt-metsző követelmények](#epic-10-kereszt-metsző-követelmények)
11. [Lefedettségi mátrix](#lefedettségi-mátrix)

---

## Epic 1: Naptár és Időszakkezelés

### US-1.1: Nem naptári rendelési időszakok létrehozása és kezelése `[A]`
**Leírás:**  
Mint **Rendszeradminisztrátor**,  
Akarok **tetszőleges kezdő- és záródátumú rendelési időszakokat létrehozni és kezelni**,  
Azért, hogy a dolgozók előre leadhassák a menürendeléseiket egy rugalmas ciklusban.

**Elfogadási Kritériumok:**
* **AC 1.1.1 (Invariánsok érvényesítése):** 
  - `StartDate <= EndDate`
  - `OrderDeadline <= ChangeDeadline(StartDate)` (a bulk ablak nem tarthat tovább, mint ameddig az első nap amúgy is módosítható).
* **AC 1.1.2 (Átfedés-védelem):** Időszakok nem fedhetik át egymást (`NOT (StartDate <= p.EndDate AND EndDate >= p.StartDate)`). A handler serializable tranzakcióban futtatja az ellenőrzést.
* **AC 1.1.3 (Határmódosítás zárolása rendelés után):** Ha az időszakhoz már tartozik rögzített rendelés, a `StartDate`, `EndDate` és `OrderDeadline` mezők nem módosíthatók, csak a `Name` és az `IsOpen`.
* **AC 1.1.4 (Rések kezelése):** Megengedett olyan munkanap, amely egyetlen időszakba sem esik. A `GetUncoveredWorkdaysQuery` képes lekérdezni ezeket a rés-napokat (US-1.5).

**Technikai hivatkozás:** `UpsertOrderingPeriodCommand`, `GetOrderingPeriodQuery`, `GetUncoveredWorkdaysQuery`

---

### US-1.2: Jövőbeli munkanap kizárása és automatikus kompenzáció `[A]`
**Leírás:**  
Mint **Rendszeradminisztrátor**,  
Akarok **egy jövőbeli munkanapot kizárttá tenni indoklással**,  
Azért, hogy aznap ne készüljön étel (pl. elmaradó munkanap, karbantartás), és az érintett rendelések automatikusan lemondásra és jóváírásra kerüljenek.

**Elfogadási Kritériumok:**
* **AC 1.2.1 (Dátumkorlát):** Csak jövőbeli nap zárható ki (`Date > Today`). Aznapi vagy múltbeli nap kizárása azonnal elutasításra kerül.
* **AC 1.2.2 (Lezárt nap védelme):** Ha a napra már létezik `KitchenClosure`, a kizárás elutasításra kerül.
* **AC 1.2.3 (Rendelések lemondása):** A naphoz tartozó összes aktív `MenuOrder` állapota `Cancelled` lesz, `CancellationReason = DayExcluded` és a konkrét `CancelledByExcludedDayId` FK beállításával.
* **AC 1.2.4 (Jóváírás és értesítés):** Minden érintett rendelés után `CancellationCredit` keletkezik a ledgerben, és a felhasználó `MenuCancelled` in-app értesítést kap.
* **AC 1.2.5 (A la carte érintetlen):** A la carte rendelés jövőbeli napra nem létezhet (csak aznapra adható le), ezért kizárásnál nincs teendő; a kizárt nap kínálata a lekérdezésekben üresen jelenik meg (AC 4.1.3).

**Technikai hivatkozás:** `ExcludeDayCommand`, `ExcludedDay` entitás

---

### US-1.3: Kizárt nap visszavonása és feltételes rendelés-helyreállítás `[A]`
**Leírás:**  
Mint **Rendszeradminisztrátor**,  
Akarok **egy kizárt napot újra megnyitni és opcionálisan visszaállítani a kizárás miatt törölt rendeléseket**,  
Azért, hogy az újra nyitva tartó konyha kiszolgálhassa az eredeti igényeket anélkül, hogy a dolgozóknak újra kellene rendelniük.

**Elfogadási Kritériumok:**
* **AC 1.3.1 (Helyreállítási feltételek):** Egy rendelés csak akkor áll vissza `Active` státuszba (`RestoreCancelledOrders = true` esetén), ha **mindegyik** feltétel teljesül:
  1. `CancellationReason == DayExcluded` ÉS `CancelledByExcludedDayId == aktuális kizárás`.
  2. A hozzá tartozó jóváírás érintetlen (`RemainingHuf == AmountHuf`).
  3. Az időszakra még nem generáltak számlát (`PeriodInvoice`).
  4. Nincs a napra `KitchenClosure`.
  5. A felhasználónak időközben nem keletkezett új aktív rendelése az adott napra.
* **AC 1.3.2 (Ledger visszavonás):** A visszaállított rendelés jóváírását a rendszer egy `CreditRevoked` negatív ledger-tétellel visszavonja (append-only), és a rendelés tulajdonosa `OrderRestored` értesítést kap.
* **AC 1.3.3 (Kihagyott rendelések kezelése):** Ha a fenti feltételek bármelyike nem teljesül, a rendelés lemondva marad, és a dolgozó `DayReopened` értesítést kap.
* **AC 1.3.4 (Önszántú lemondás védelme):** Felhasználó által saját döntésből lemondott rendelés (`CancellationReason == ByUser`) soha nem éledhet újra.
* **AC 1.3.5 (Adminisztrátori visszajelzés):** A parancs visszaadja a `RestoredCount` és `SkippedCount` értéket, valamint a kihagyás okainak bontását, hogy az admin lássa, mi történt.

**Technikai hivatkozás:** `RemoveExcludedDayCommand`, `Result<RemoveExcludedDayResult>`

---

### US-1.4: Rendelési időszakok megtekintése és dátum → időszak feloldás `[A/U]`
**Leírás:**  
Mint **Dolgozó vagy Rendszeradminisztrátor**,  
Akarok **listázni a rendelési időszakokat, és megtudni, hogy egy adott nap melyik időszakba esik**,  
Azért, hogy tudjam, mikor és mire adhatok le rendelést, illetve hogy a rendszer a rendeléshez és a számlázáshoz egyértelműen hozzá tudja rendelni az időszakot.

**Elfogadási Kritériumok:**
* **AC 1.4.1 (Listázás):** Az időszakok `StartDate` szerint rendezve kérhetők le, `Name`, `StartDate`, `EndDate`, `OrderDeadline` és `IsOpen` mezőkkel; egy időszak azonosító alapján önmagában is lekérhető.
* **AC 1.4.2 (Egyértelmű feloldás):** Egy adott dátumra a lekérdezés **legfeljebb egy** időszakot ad vissza — az átfedés tilalma (AC 1.1.2) miatt kettő soha nem fordulhat elő.
* **AC 1.4.3 (Lefedetlen nap):** Ha a dátum egyetlen időszakba sem esik, a válasz explicit „nincs lefedve" jelzés, nem hiba és nem üres találat — a hívó use case-ek (menürendelés, a la carte) ez alapján utasítanak el `OutsidePeriod` okkal.

**Technikai hivatkozás:** `GetOrderingPeriodQuery`, `GetOrderingPeriodsQuery`, `GetOrderingPeriodForDateQuery`

---

### US-1.5: Lefedetlen munkanapok riportja `[A]`
**Leírás:**  
Mint **Rendszeradminisztrátor**,  
Akarok **lekérdezni azokat a munkanapokat, amelyek egyetlen rendelési időszakba sem esnek**,  
Azért, hogy észrevegyem a véletlenül kihagyott napokat, mielőtt a dolgozók hiába próbálnának rájuk rendelni.

**Elfogadási Kritériumok:**
* **AC 1.5.1 (Tartomány szerinti riport):** A lekérdezés egy megadott dátumtartomány azon **munkanapjait** adja vissza, amelyek egyetlen `OrderingPeriod` `[StartDate, EndDate]` tartományába sem esnek.
* **AC 1.5.2 (Zajszűrés):** A hétvégék és a kizárt napok nem jelennek meg a riportban — azokon amúgy sincs kiszolgálás.
* **AC 1.5.3 (A rés nem hiba):** A rés létezése megengedett állapot (AC 1.1.4); a riport csak láthatóvá teszi, nem blokkol és nem javít automatikusan.

**Technikai hivatkozás:** `GetUncoveredWorkdaysQuery`

---

### US-1.6: Kizárt napok listázása `[A/U]`
**Leírás:**  
Mint **Dolgozó vagy Rendszeradminisztrátor**,  
Akarok **egy időszakra lekérdezni a kizárt napokat**,  
Azért, hogy lássam, mely napokon nem lesz kiszolgálás, és értsem, miért nem rendelhetők.

**Elfogadási Kritériumok:**
* **AC 1.6.1:** A lekérdezés egy dátumtartományra adja vissza a kizárt napokat `Date` és `Reason` mezőkkel.
* **AC 1.6.2 (Auditálhatóság):** A találatok tartalmazzák a kizárást felvivő felhasználót (`CreatedByUserId`) és a felvitel időpontját (`CreatedAtUtc`).

**Technikai hivatkozás:** `GetExcludedDaysQuery`, `ExcludedDay`

---

### US-1.7: Rendelhető és lemondható napok lekérdezése — a felület igazságforrása `[U]`
**Leírás:**  
Mint **Dolgozó**,  
Akarok **naponkénti bontásban látni, hogy melyik napra rendelhetek, melyiket mondhatom le, és ha nem, miért nem**,  
Azért, hogy a felület előre letiltsa a nem választható napokat, és ne utólag, hibaüzenetben szembesüljek a korlátokkal.

**Elfogadási Kritériumok:**
* **AC 1.7.1 (Naponkénti válasz):** A lekérdezés az időszak minden napjára visszaadja az `orderable` és a `cancellable` jelzőt.
* **AC 1.7.2 (Indoklás egységes kóddal):** Ha egy nap nem rendelhető vagy nem mondható le, a válasz megadja az okot **ugyanabból az `ErrorCodes` készletből**, amit a köteges parancsok `Skipped` listája használ (`DeadlinePassed`, `DayClosed`, `DayExcluded`, `NotWorkingDay`, `MenuNotPublished`, `OutsidePeriod`, `AlreadyOrdered`, `NoActiveOrder`).
* **AC 1.7.3 (Fázisfüggő eredmény):** A válasz tükrözi az aktuális rendelési fázist: az `OrderDeadline` előtt (A fázis) az időszak minden alkalmas napja rendelhető, utána (B fázis) csak a 3 munkanapos szabálynak megfelelők.
* **AC 1.7.4 (Konzisztencia a parancsokkal):** Egy nap akkor és csak akkor kap `orderable = true` / `cancellable = true` értéket, ha a `PlacePeriodOrderCommand` / `CancelMenuOrdersCommand` ugyanarra a napra ugyanabban a pillanatban sikerrel járna — a parancs eredménye megerősítés, nem meglepetés.

**Technikai hivatkozás:** `GetOrderableDaysQuery`, `ErrorCodes`

---

## Epic 2: Étlap- és Variánskezelés

### US-2.1: Napi menü rögzítése `[A]`
**Leírás:**  
Mint **Rendszeradminisztrátor**,  
Akarok **napi A/B/C menüket rögzíteni**,  
Azért, hogy a dolgozók láthassák a kínálatot és megrendelhessék az ebédet.

**Elfogadási Kritériumok:**
* **AC 2.1.1:** Egy naphoz több variáns (pl. "A", "B", "C") rögzíthető `Code`, `Name` (leves), `Description` (főétel) mezőkkel; a `SortOrder`-t a felület a `Code` ábécésorrendjéből számítja, nem kér be külön mezőt.
* **AC 2.1.2:** Nincs külön publikálás-lépés: az `UpsertDailyMenuCommand` sikeres lefutása (ehhez legalább egy variáns szükséges) azonnal rendelhetővé teszi a napot a dolgozók számára (`IsPublished = true`).
* **AC 2.1.3:** A menü ára snapshotolódik a rendeléskor (fix alapérték: 1400 Ft).
* **AC 2.1.4 (Leves/főétel kizárólag katalógusból):** A leves és a főétel mező a `MenuDish` katalógusból választható autocomplete-tel (`GetMenuDishSuggestionsQuery`) — egyedi, a katalógusban még nem szereplő név **nem** írható be közvetlenül ide, a mező a be nem azonosítható értéket elveti. Egy teljesen új étel felvitele egy külön "+ Új étel" képernyőn (`AddMenuDishDialog` → `CreateMenuDishCommand`) történik, amit a napi menü szerkesztő dialógus nem tartalmaz — ennek admin felületi bekötése még nyitott teendő (lásd `03-nyitott-teendok.md`).
* **AC 2.1.5 (Allergének és tápértékek az ételhez tárolva):** Leveshez és főételhez külön allergén-lista, valamint 7 tápérték-mező (energia kcal, zsír, telített zsír, szénhidrát, cukor, fehérje, só — az energia kivételével mind gramm) adható meg; ez az adott **ételnévhez** (nem a naphoz vagy a variánshoz) tárolódik, így egy korábban már megadott étel újbóli kiválasztásakor az allergénje és a tápértéke magától megjelenik, és a dolgozói „napi kiírás" (US-2.6) mellette mutatja.
* **AC 2.1.6 (Leves-ismétlés A-ról):** Mivel az A és a B menü levese jellemzően megegyezik, ha az admin megadja az A variáns levesét, a felület felajánlja azt minden még üres leves-mezővel rendelkező variánsnak is — felülírható.

**Technikai hivatkozás:** `UpsertDailyMenuCommand`, `GetMenuDishSuggestionsQuery`, `CreateMenuDishCommand`, `UpdateMenuDishCommand`, `DailyMenu`, `MenuVariant`, `MenuDish`

---

### US-2.2: Menüvariáns törlése automatikus átvezetéssel `[A]`
**Leírás:**  
Mint **Rendszeradminisztrátor**,  
Akarok **egy kikerülő menüvariánst törölni az étlapról**,  
Azért, hogy a már leadott rendelések automatikusan átkerüljenek az első elérhető alapértelmezett variánsra ("A" menü).

**Elfogadási Kritériumok:**
* **AC 2.2.1 (Átvezetés):** Ha a napon marad másik variáns, az érintett aktív rendelések átkerülnek a legkisebb `SortOrder`/kód szerinti variánsra. A rendszer naplózza az eredeti kódot (`ReassignedFromVariantCode`) és az időpontot.
* **AC 2.2.2 (Értesítés küldése):** Az átvezetésről a rendelés tulajdonosa és (ha más adta le) a leadója is `OrderReassigned` in-app értesítést kap.
* **AC 2.2.3 (Utolsó variáns esete):** Ha nem marad más variáns a napon, az összes rendelés automatikusan lemondásra kerül (`CancellationReason = VariantRemoved`), jóváírás keletkezik és `MenuCancelled` értesítés megy ki.
* **AC 2.2.4 (Lezárt nap védelme):** Ha a napra `KitchenClosure` létezik, a variáns törlése elutasításra kerül.

**Technikai hivatkozás:** `DeleteMenuVariantCommand`, `IMenuReassignmentService`

---

### US-2.3: Napi menü módosítása értesítéssel `[A]`
**Leírás:**  
Mint **Rendszeradminisztrátor**,  
Akarok **egy már rögzített nap menüjét módosítani (variáns neve, leírása, sorrendje)**,  
Azért, hogy a változásról az aznapra már rendelő dolgozók értesüljenek, anélkül hogy a rendelésük feleslegesen mozogna.

**Elfogadási Kritériumok:**
* **AC 2.3.1 (Nincs felesleges átvezetés):** Ha csak a variáns neve, leírása vagy sorrendje változik, a leadott rendelések **változatlanok** maradnak — nincs átvezetés és nincs lemondás.
* **AC 2.3.2 (Értesítés):** A nap aktív rendelői `MenuChanged` in-app értesítést kapnak a változásról.
* **AC 2.3.3 (Variáns kikerülése):** Ha a módosítás egy variáns eltávolítását jelenti, arra a US-2.2 átvezetési szabálya érvényes (átvezetés az első maradék variánsra, vagy lemondás, ha nem marad).
* **AC 2.3.4 (Lezárt nap védelme):** Ha a napra `KitchenClosure` létezik, a módosítás elutasításra kerül.

**Technikai hivatkozás:** `UpsertDailyMenuCommand` (módosítási ág), `IMenuReassignmentService`

---

### US-2.4: Teljes napi menü törlése `[A]`
**Leírás:**  
Mint **Rendszeradminisztrátor**,  
Akarok **egy nap teljes menüjét törölni**,  
Azért, hogy a tévesen felvitt vagy elmaradó napokat kivezethessem, az érintett dolgozók pedig automatikusan jóváírást kapjanak.

**Elfogadási Kritériumok:**
* **AC 2.4.1 (Rendelések lemondása):** A nap összes aktív rendelése lemondásra kerül `CancellationReason = MenuDeleted` okkal.
* **AC 2.4.2 (Jóváírás és értesítés):** Minden érintett rendelés után `CancellationCredit` keletkezik, és a dolgozó `MenuCancelled` értesítést kap.
* **AC 2.4.3 (Lezárt nap védelme):** Ha a napra `KitchenClosure` létezik, a törlés elutasításra kerül.
* **AC 2.4.4 (Elhatárolás a kizárástól):** A menü törlése nem teszi kizárt nappá a napot — ha a cél az, hogy aznap egyáltalán ne legyen kiszolgálás, az `ExcludeDayCommand` (US-1.2) a helyes eszköz.

**Technikai hivatkozás:** `DeleteDailyMenuCommand`, `CancellationReason.MenuDeleted`

---

### US-2.5: Étlap megtekintése napra és időszakra `[A/U]`
**Leírás:**  
Mint **Dolgozó vagy Rendszeradminisztrátor**,  
Akarok **egy nap vagy egy teljes időszak étlapját lekérdezni**,  
Azért, hogy a dolgozó lássa a rendelhető kínálatot, az admin pedig a szerkesztés alatt álló napokat is.

**Elfogadási Kritériumok:**
* **AC 2.5.1 (Rendezés):** A variánsok `SortOrder`, azonos érték esetén `Code` szerint rendezve jelennek meg.
* **AC 2.5.2 (Publikálási láthatóság):** `[U]` felé csak a publikált menü (`IsPublished = true`) látszik; `[A]` a nem publikált, szerkesztés alatti napokat is látja.
* **AC 2.5.3 (Időszaki nézet):** Az időszaki lekérdezés adja a rendelőfelület adatát: az időszak minden napjára a variánsok, összefésülve a nap rendelhetőségi állapotával (US-1.7).

**Technikai hivatkozás:** `GetDailyMenuQuery`, `GetPeriodMenuQuery`

---

### US-2.6: Napi kiírás — mai menü és a saját választásom `[U]`
**Leírás:**  
Mint **Dolgozó**,  
Akarok **egy képernyőn látni a mai menüt, a saját mai választásomat és a mai a la carte kínálatot**,  
Azért, hogy reggel egy pillantással tudjam, mit fogok ma enni, és mit tudok még rendelni.

**Elfogadási Kritériumok:**
* **AC 2.6.1 (Mai menü):** A válasz tartalmazza a mai nap publikált menüvariánsait.
* **AC 2.6.2 (Saját választás explicit jelzéssel):** A válasz megadja a felhasználó mai aktív menürendelését, **vagy explicit „ma nem rendeltél" jelzést** — az üres mező nem elfogadható megoldás, mert nem különböztethető meg a hiányzó adattól.
* **AC 2.6.3 (A la carte rész):** A válasz tartalmazza a mai a la carte kínálatot szabad kerettel és a felhasználó mai a la carte rendelését.
* **AC 2.6.4 (Nem rendelhető nap):** Ha a mai nap hétvége, kizárt nap, vagy nincs rá publikált menü, a válasz ezt jelzi, nem hibázik.

**Technikai hivatkozás:** `GetTodayMenuForUserQuery`

---

## Epic 3: Menü Előrendelés és Lemondás

### US-3.1: Kétfázisú köteges menürendelés leadása `[U]`
**Leírás:**  
Mint **Dolgozó**,  
Akarok **egyszerre több napra menüt rendelni saját magam vagy egy kollégám nevében**,  
Azért, hogy egyszerűen biztosítsam az ebédet az egész időszakra vagy a pótlólagosan elérhető napokra.

**Elfogadási Kritériumok:**
* **AC 3.1.1 (A Fázis — Bulk rendelés):** Ha `now <= OrderDeadline` és az időszak `IsOpen`, bármelyik időszaki publikált munkanapra lehet rendelni átfutási korlát nélkül.
* **AC 3.1.2 (B Fázis — Pótlólagos rendelés):** Ha `now > OrderDeadline`, a rendelés akkor engedélyezett, ha az időszak `IsOpen`, `Today <= EndDate`, és a napra érvényes a 3 munkanapos szabály: `now <= ChangeDeadline(Date)` és nincs `KitchenClosure`. Akár az összes hátralévő nap megrendelhető egy hívással.
* **AC 3.1.3 (Részleges siker és kötegelés):** A leadott dátumlista feldolgozása nem "mindent vagy semmit" alapon működik. A sikeres napok egy tranzakcióban mentődnek (`Succeeded`), az elutasított napok pedig pontos hibakóddal a `Skipped` listába kerülnek.
* **AC 3.1.4 (Felületi visszajelzés szabálya):** Ha a `Skipped` lista nem üres, a művelet nem jelezhető tisztán sikeresnek a felületen; a kimaradt napokat és az okokat kötelező megjeleníteni.
* **AC 3.1.5 (Napi 1 adag limit):** Egy felhasználónak egy napra legfeljebb 1 aktív menürendelése lehet (szűrt unique index `(UserId, Date) WHERE Status = 0`).
* **AC 3.1.6 (Más nevében rendelés):** Rendelés leadható más nevében (`TargetUserId != CurrentUser`), de a rendszer mindig auditálja a tényleges leadót (`PlacedByUserId`).
* **AC 3.1.7 (Időszakhoz kötés):** Minden rendelt dátumnak az időszak `[StartDate, EndDate]` tartományába kell esnie (`OutsidePeriod` egyébként), és a létrejövő rendelésre rákerül az `OrderingPeriodId` — így a későbbi határmódosítás nem sodorja át a rendelést másik számlára.

**Technikai hivatkozás:** `PlacePeriodOrderCommand`, `Result<BatchOrderResult>`, `GetOrderableDaysQuery`

---

### US-3.2: Menürendelés köteges lemondása 3 munkanapos szabállyal `[U]`
**Leírás:**  
Mint **Dolgozó**,  
Akarok **egy vagy több korábban megrendelt menüt lemondani**,  
Azért, hogy a távollétem idejére ne készüljön feleslegesen étel és az összeg jóváírásra kerüljön.

**Elfogadási Kritériumok:**
* **AC 3.2.1 (Lemondási határidő):** A lemondás feltétele: `now <= ChangeDeadline(Date)` ÉS nincs `KitchenClosure(Date)`. (Példa: csütörtöki ebéd lemondási határideje a megelőző hétfő 11:00 helyi idő szerint).
* **AC 3.2.2 (Aznapi lemondás tiltása):** Aznapi menürendelés lemondása szigorúan tilos és nem lehetséges.
* **AC 3.2.3 (Jóváírás és audit):** A lemondott rendelés `Status = Cancelled`, `CancellationReason = ByUser` állapotot kap, és azonnal létrejön a hozzá tartozó `CancellationCredit` ledger tétel.
* **AC 3.2.4 (Köteges lemondás részleges sikerrel):** A parancs **dátumlistát** fogad, és ugyanúgy `Result<BatchOrderResult>` értéket ad vissza, mint a rendelés: a sikeres napok egy tranzakcióban mentődnek (`Succeeded`), a kihagyottak a `Skipped` listába kerülnek `DeadlinePassed` / `DayClosed` / `NoActiveOrder` okkal. Egyetlen nap lemondása ennek az egyelemű esete — nincs rá külön parancs.
* **AC 3.2.5 (Felületi visszajelzés szabálya):** Ha a `Skipped` lista nem üres, a lemondás nem jelezhető tisztán sikeresnek; a kimaradt napokat és az okukat kötelező megjeleníteni (ugyanaz a szabály, mint AC 3.1.4). Ellenkező esetben a dolgozó abban a hitben marad, hogy lemondta az ebédjét, miközben az elkészül és kiszámlázásra kerül.
* **AC 3.2.6 (A bulk ablak nem ad kedvezményt):** A lemondásra mindkét rendelési fázisban ugyanaz a `ChangeDeadline` szabály vonatkozik — az `OrderDeadline` előtti időszak sem enged közelebbi napot lemondani.

**Technikai hivatkozás:** `CancelMenuOrdersCommand`, `IWorkingDayCalculator`, `Result<BatchOrderResult>`

---

### US-3.3: Saját időszaki rendeléseim megtekintése `[U]`
**Leírás:**  
Mint **Dolgozó**,  
Akarok **egy időszakra összesítve látni a saját menürendeléseimet**,  
Azért, hogy ellenőrizhessem, mely napokra és milyen menüre rendeltem, és mi lett a lemondásaimmal.

**Elfogadási Kritériumok:**
* **AC 3.3.1 (Napok és variánsok):** A válasz az időszak napjaira megadja az aktív rendelést a választott variáns kódjával és nevével.
* **AC 3.3.2 (Lemondott napok):** A lemondott rendelések is megjelennek a lemondás okával (`ByUser`, `DayExcluded`, `MenuDeleted`, `VariantRemoved`), hogy a dolgozó lássa, mi nem az ő döntése volt.
* **AC 3.3.3 (Leadó feltüntetése):** Ha a rendelést más adta le (`PlacedByUserId != UserId`), a leadó neve is megjelenik.
* **AC 3.3.4 (Átvezetés láthatósága):** Ha a rendelés variánsa átvezetésre került, az eredeti kód (`ReassignedFromVariantCode`) is látszik.

**Technikai hivatkozás:** `GetMyPeriodOrderQuery`

---

### US-3.4: Rendelések adminisztrátori lekérdezése `[A]`
**Leírás:**  
Mint **Rendszeradminisztrátor**,  
Akarok **szűrhetően lekérdezni a rendeléseket**,  
Azért, hogy reklamáció vagy elszámolási kérdés esetén vissza tudjam keresni, ki mit rendelt és ki adta le.

**Elfogadási Kritériumok:**
* **AC 3.4.1 (Szűrők):** A lekérdezés szűrhető rendelési időszakra, felhasználóra és státuszra (`Active` / `Cancelled`).
* **AC 3.4.2 (Audit mezők):** A találatok tartalmazzák a leadót (`PlacedByUserId`), a leadás időpontját, és lemondás esetén a lemondás okát, idejét és a lemondó személyt.

**Technikai hivatkozás:** `GetUserOrdersQuery`

---

## Epic 4: Aznapi A La Carte Rendelés és Készletfoglalás

### US-4.1: Napi a la carte kínálat és keret beállítása `[A]`
**Leírás:**  
Mint **Rendszeradminisztrátor**,  
Akarok **aznapi a la carte ételeket és adagkeretet (`Capacity`) rögzíteni**,  
Azért, hogy a konyha kapacitásának megfelelően korlátozzam a rendelhető adagszámot.

**Elfogadási Kritériumok:**
* **AC 4.1.1 (Kategóriánkénti napi keret):** Az admin kategóriánként (Leves, Főétel, Köret, Desszert, Öntet) adhat meg napi ajánlatot az aktív törzsadatokhoz.
* **AC 4.1.2:** A `Capacity` nem csökkenthető a már lefoglalt (`OrderedCount`) alá — Leves ajánlatra ez nem értelmezett, mert az `OrderedCount` rá nézve sosem növekszik (AC 4.2.4).
* **AC 4.1.3:** Kizárt napra a lekérdezések automatikusan üres kínálatot adnak vissza.
* **AC 4.1.4 (Legfeljebb egy Leves ajánlat naponta):** Leves kategóriájú tételre egy napra legfeljebb egy aktív ajánlat rögzíthető; egy második felvétele ugyanarra a napra elutasításra kerül — ez teszi egyértelművé, melyik leves ára adódik hozzá az aznapi főétel-rendelésekhez.

**Technikai hivatkozás:** `SetDailyOfferCommand`, `ALaCarteDailyOffer`

---

### US-4.2: Versenyhelyzet-biztos aznapi a la carte vásárlás `[U]`
**Leírás:**  
Mint **Dolgozó**,  
Akarok **aznap délelőtt 10:30-ig a la carte ételeket rendelni a készlet erejéig**,  
Azért, hogy a napi menü helyett vagy mellett egyéb ételeket fogyaszthassak.

**Elfogadási Kritériumok:**
* **AC 4.2.1 (Időkorlát):** Rendelés csak aznap (munkanapon), legkésőbb helyi idő szerint 10:30-ig adható le.
* **AC 4.2.2 (Időszaki fedettség):** Az adott napnak bele kell esnie egy létező `OrderingPeriod` tartományába (hogy legyen mihez számlázni).
* **AC 4.2.3 (Darabszám limit — tételenként, nem kategóriánként):** Egy felhasználó **tételenként** legfeljebb 1 darabot rendelhet aznapra — ugyanazon a napon **több különböző Főétel** tétel is megrendelhető (mindegyikből legfeljebb 1 db), csak ugyanazon tétel duplikálása tilos.
* **AC 4.2.4 (Atomi készletfoglalás — Leves kivételével):** A foglalás egyetlen atomi feltételes SQL UPDATE-tel történik (`OrderedCount < Capacity`), **minden nem Leves kategóriájú tételre**. Ha bármely nem Leves tétel elfogyott, a tranzakció visszaáll (nincs részleges a la carte rendelés). Leves kategóriájú ajánlatra nincs foglalás — az korlátlan (AC 4.2.8), és rá közvetlen rendelés nem is adható le.
* **AC 4.2.5 (Lemondás tiltása):** A leadott a la carte rendelések nem mondhatók le.
* **AC 4.2.6 (Az `IsOpen` és az `OrderDeadline` itt nem feltétel):** Ez aznapi vásárlás, nem előrendelés — a rendelési időszak csak a számlázási hovatartozás (`OrderingPeriodId`) miatt kell. Lezárt (`IsOpen = false`) vagy a leadási határidején túli időszak napján is leadható a la carte rendelés.
* **AC 4.2.7 (Kizárt és lefedetlen nap):** Kizárt napra nem adható le rendelés (a kínálat is üres, AC 4.1.3); lefedetlen (rés-)napon a rendelés `OutsidePeriod` okkal elutasításra kerül.
* **AC 4.2.8 (Leves a főétel árába rejtve, korlátlanul):** Leves kategóriájú ajánlatra közvetlen rendelés nem adható le — a felület nem is kínálja fel önálló, árazott sorként. Amikor a dolgozó egy Főétel-tételt rendel, a rendelési sor `UnitPriceHuf`-ja a főétel árának és az aznapi aktív Leves-ajánlat árának **összege**, egyetlen kombinált számként; a sor `IncludesSoup` mezője ekkor snapshotolódik, és a felület ebből (nem élő állapotból) jeleníti meg a „(levessel)" jelzést. Ha aznapra nincs Leves-ajánlat, a kombinált ár a puszta főétel ára (0 Ft leves-rész, nem hibaeset).

**Technikai hivatkozás:** `PlaceALaCarteOrderCommand`, `ALaCarteOrder`, `ALaCarteOrderLine`

---

### US-4.3: A la carte törzsadat kezelése `[A]`
**Leírás:**  
Mint **Rendszeradminisztrátor**,  
Akarok **a la carte ételeket felvinni, módosítani és kivezetni**,  
Azért, hogy a napi kínálatot egy karbantartott törzsadatból tudjam összeállítani.

**Elfogadási Kritériumok:**
* **AC 4.3.1 (Törzsadat mezők):** Egy tétel `Name`, `Category` (Leves / Főétel / Köret / Desszert / Öntet), `PriceHuf`, `IsActive`, valamint — a `MenuDish` mintáját követő — 7 opcionális tápérték-mező adható meg. Ez a katalógus **nem osztozik** a `MenuDish` katalóguson.
* **AC 4.3.2 (Kivezetés, nem törlés):** A kivezetés az `IsActive = false` beállítása; az inaktív tétel új napi ajánlatba nem vehető fel.
* **AC 4.3.3 (Múlt védelme):** A kivezetés és az árváltozás nem érinti a már leadott rendeléseket és a korábbi számlákat — a rendelési sorok a nevet, a kategóriát és az egységárat snapshotként tárolják.
* **AC 4.3.4 (Listázás):** A törzsadat kategória szerint csoportosítva, aktív/inaktív szűrővel lekérdezhető.

**Technikai hivatkozás:** `UpsertALaCarteItemCommand`, `DeactivateALaCarteItemCommand`, `GetALaCarteItemsQuery`, `ALaCarteItem`

---

### US-4.4: Napi ajánlat visszavonása `[A]`
**Leírás:**  
Mint **Rendszeradminisztrátor**,  
Akarok **egy tévesen felvitt napi ajánlatot visszavonni**,  
Azért, hogy csak az valóban elérhető ételek jelenjenek meg a dolgozóknak.

**Elfogadási Kritériumok:**
* **AC 4.4.1 (Csak érintetlen ajánlat):** Az ajánlat csak akkor törölhető, ha még nincs rá rendelés (`OrderedCount == 0`).
* **AC 4.4.2 (Elutasítás rendelés esetén):** Ha már van foglalás, a törlés elutasításra kerül — a keret csökkentésére az AC 4.1.2 szabálya érvényes (a lefoglalt darabszám alá nem vihető).

**Technikai hivatkozás:** `RemoveDailyOfferCommand`

---

### US-4.5: Napi a la carte kínálat megtekintése szabad kerettel `[A/U]`
**Leírás:**  
Mint **Dolgozó vagy Rendszeradminisztrátor**,  
Akarok **látni egy nap a la carte kínálatát a még elérhető darabszámmal**,  
Azért, hogy tudjam, mit lehet még rendelni, mielőtt leadom a rendelést.

**Elfogadási Kritériumok:**
* **AC 4.5.1 (Szabad keret — Leves kivételével):** A **nem Leves** tételeknél megjelenik a szabad darabszám (`Capacity − OrderedCount`), kategória szerint csoportosítva. A Leves ajánlat ára a Főétel-sorok kombinált árában jelenik meg (AC 4.2.8), önálló sorként soha.
* **AC 4.5.2 (Kizárt nap):** Kizárt napra a lekérdezés üres listát ad (AC 4.1.3); a kizárás visszavonása után az ajánlatok maguktól újra megjelennek — nincs külön aktiválási lépés.
* **AC 4.5.3 (Nem garancia):** A szabad keret pillanatkép; a tényleges foglalást a rendelés atomi UPDATE-je dönti el (AC 4.2.4), ezért a felület nem kezelheti foglalásnak.

**Technikai hivatkozás:** `GetDailyOffersQuery`

---

### US-4.6: Aznapi a la carte konyhai lista `[A]`
**Leírás:**  
Mint **Rendszeradminisztrátor / Konyhai partner**,  
Akarok **tételenkénti összesítést kapni az aznapi a la carte rendelésekről**,  
Azért, hogy a konyha pontosan tudja, miből hány adagot kell elkészítenie.

**Elfogadási Kritériumok:**
* **AC 4.6.1:** A lista tételenként adja a megrendelt darabszámot, kategória szerint csoportosítva.
* **AC 4.6.2:** Az összesítés a rendeléskori snapshot neveket használja, így egy időközbeni átnevezés nem írja át a mai listát.
* **AC 4.6.3 (Levesadag — levezetett érték):** A lista feltünteti az aznap elkészítendő levesadagok számát is; ez **nem tárolt sorból**, hanem az aznapi, `CategorySnapshot == Foetel` rendelési sorok darabszámából származik (minden rendelt főétel egy tányér levest jelent).
* **AC 4.6.4 (Havi bontás):** Az admin felület napi nézet mellett havi összesítőt is tud mutatni ugyanazzal a tételenkénti/levesadag-logikával, egy adott hónap összes napjára összevonva — visszamenőleges rendelési igény kiszolgálására.

**Technikai hivatkozás:** `GetALaCarteDailySummaryQuery`, `GetALaCarteMonthlySummaryQuery`

---

## Epic 5: Jóváírás-könyvelés (Ledger) és Egyenlegkezelés

### US-5.1: Jóváírás-egyenleg megtekintése `[U]`
**Leírás:**  
Mint **Dolgozó**,  
Akarok **bármikor látni az aktuális jóváírás-egyenlegemet**,  
Azért, hogy tudjam, mennyi felhasználható összeg áll rendelkezésemre a következő menüszámlámhoz.

**Elfogadási Kritériumok:**
* **AC 5.1.1 (Azonnali egyenleg):** Az egyenleg az aktív tételek összegét mutatja (`Σ RemainingHuf`). Nincs várakozási idő (`EligibleFrom`), a jóváírás a keletkezés pillanatától él.
* **AC 5.1.2 (Menü-hatókör kimondása):** Az egyenleg **kizárólag menürendelésre** számítható be; a felület ezt egyértelműen jelzi, nehogy a dolgozó a la carte fedezetnek higgye.

**Technikai hivatkozás:** `GetMyBalanceQuery`, `CreditEntry`

---

### US-5.2: Kézi jóváírás rögzítése indoklással `[A]`
**Leírás:**  
Mint **Rendszeradminisztrátor**,  
Akarok **manuális jóváírást jóváírni egy dolgozónak indoklással ellátva**,  
Azért, hogy kompenzáljam az esetleges aznapi konyhai üzemzavarokat vagy egyéb reklamációkat.

**Elfogadási Kritériumok:**
* **AC 5.2.1:** Az admin megadja a felhasználót, az összeget és a kötelező szöveges indoklást.
* **AC 5.2.2:** Létrejön egy `ManualAdjustment` típusú `CreditEntry`, ami azonnal bekerül a felhasználó menü-egyenlegébe.
* **AC 5.2.3 (Audit):** A tétel rögzíti a rögzítő adminisztrátort (`CreatedByUserId`) és az időpontot; a ledger append-only, a tétel utólag nem módosítható.

**Technikai hivatkozás:** `AddManualCreditCommand`

---

### US-5.3: Tételes jóváírás-kimutatás (ledger) `[U]`
**Leírás:**  
Mint **Dolgozó**,  
Akarok **rálátni a jóváírásaim mögötti tételes könyvelésre**,  
Azért, hogy pontosan kövessem, miből keletkezett az egyenlegem és mikor, melyik számlából vonták le.

**Elfogadási Kritériumok:**
* **AC 5.3.1 (Ledger kimutatás):** A kimutatás tételesen mutatja:
  - Mi lett lemondva (`SourceMenuOrderId` — innen a dátum és a variáns is olvasható),
  - Mennyi jóváírás keletkezett (`CancellationCredit`),
  - Melyik időszaki számla menürészéből vonódott le (`CreditApplied` + `PeriodInvoiceId`),
  - Történt-e visszavonás (`CreditRevoked`) vagy kézi korrekció (`ManualAdjustment`).
* **AC 5.3.2 (Visszavonás láthatósága):** A `CreditRevoked` tételek is megjelennek az indoklásukkal — enélkül a dolgozó szempontjából magyarázat nélkül csökkenne az egyenlege.
* **AC 5.3.3 (Append-only nézet):** A kimutatás soha nem mutat módosított vagy eltüntetett tételt; a korrekció mindig új, ellentétes előjelű sorként jelenik meg.

**Technikai hivatkozás:** `GetMyCreditLedgerQuery`, `CreditEntry`

---

## Epic 6: Konyhai Összesítés és Napzárás

### US-6.1: Konyhai rendelés-összesítő lekérése `[A]`
**Leírás:**  
Mint **Rendszeradminisztrátor / Konyhai partner**,  
Akarok **élő összesítést látni a variánsonkénti menükről és a la carte tételekről**,  
Azért, hogy a konyha pontosan megtervezhesse az alapanyag-beszerzést és a főzést.

**Elfogadási Kritériumok:**
* **AC 6.1.1:** Lekérhető egyetlen nap élő adagszáma variánsonként (A/B/C); az aznapi a la carte összesítést a US-4.6 adja.
* **AC 6.1.2:** Lekérhető időszaki tartomány összesítője is a korábbi rendelési igények kiszolgálására.
* **AC 6.1.3 (Csak aktív rendelés):** Az összesítő kizárólag az `Active` státuszú rendeléseket számolja; a lemondottak nem jelennek meg benne.

**Technikai hivatkozás:** `GetKitchenSummaryQuery`, `GetKitchenSummaryRangeQuery`

---

### US-6.2: Nap végleges lezárása és feloldása `[A]`
**Leírás:**  
Mint **Rendszeradminisztrátor**,  
Akarok **egy adott napot konyhai szempontból lezárni (összesítő elküldve snapshot)**,  
Azért, hogy rögzítsem a rendelési állományt és megakadályozzam a további módosításokat.

**Elfogadási Kritériumok:**
* **AC 6.2.1 (Snapshot mentése):** A `CloseDayCommand` elmenti a `KitchenClosure` és `KitchenClosureLine` rekordokat a záráskori pontos darabszámokkal.
* **AC 6.2.2 (Zárolási hatás):** A lezárt napra a 3 munkanapos határidőtől függetlenül **tilos a további rendelés és lemondás**.
* **AC 6.2.3 (Újranyitás):** A `ReopenDayCommand` feloldja a zárolást; amennyiben a 3 munkanapos határidő még engedi, a nap újra módosíthatóvá válik.
* **AC 6.2.4 (Menüszerkesztés zárolása):** Lezárt napon a menü módosítása, variáns törlése és a nap menüjének törlése is elutasításra kerül (AC 2.2.4, AC 2.3.4, AC 2.4.3), a nap kizárása szintén (AC 1.2.2).

**Technikai hivatkozás:** `CloseDayCommand`, `ReopenDayCommand`, `KitchenClosure`

---

### US-6.3: Napzárás pillanatképének lekérdezése `[A]`
**Leírás:**  
Mint **Rendszeradminisztrátor**,  
Akarok **visszanézni, hogy egy nap zárásakor pontosan mi ment ki a konyhának**,  
Azért, hogy egy utólagos eltérés esetén bizonyítható legyen a leadott összesítő tartalma.

**Elfogadási Kritériumok:**
* **AC 6.3.1:** A lekérdezés visszaadja a záráskori variánsonkénti darabszámokat (`KitchenClosureLine`), a teljes adagszámot, a záró személyt és a zárás időpontját.
* **AC 6.3.2 (Audit megőrzés):** A snapshot a `ReopenDayCommand` után is megmarad — az újranyitás nem törli a korábbi zárás bizonyítékát.

**Technikai hivatkozás:** `GetKitchenClosureQuery`, `KitchenClosure`, `KitchenClosureLine`

---

## Epic 7: Elszámolás és Számlázás

### US-7.1: Időszaki számlák generálása szigorú menü-jóváírás beszámítással `[A]`
**Leírás:**  
Mint **Rendszeradminisztrátor**,  
Akarok **időszaki számlákat generálni a dolgozók számára a jóváírások automatikus elszámolásával**,  
Azért, hogy mindenki a ténylegesen fizetendő, korrigált összeget kapja meg.

**Elfogadási Kritériumok:**
* **AC 7.1.1 (Időszak alapú gyűjtés):** A számla nem naptári hónap, hanem a rendeléskor rögzített `OrderingPeriodId` alapján gyűjti össze a tételeket.
* **AC 7.1.2 (Szigorú menü-hatókör):** A felhalmozott jóváírás **kizárólag a menütételek bruttó összegéből (`MenuGrossHuf`) vonható le** FIFO sorrendben.
* **AC 7.1.3 (A la carte elkülönítés):** Az a la carte összeg (`ALaCarteGrossHuf`) teljes egészében fizetendő marad, jóváírás azt nem csökkentheti:
  - `CreditAppliedHuf <= MenuGrossHuf`
  - `MenuPayableHuf = MenuGrossHuf - CreditAppliedHuf`
  - `ALaCartePayableHuf = ALaCarteGrossHuf`
  - `PayableHuf = MenuPayableHuf + ALaCartePayableHuf`
* **AC 7.1.4 (Görgetés):** Ha az egyenleg meghaladja a menü bruttó összegét, a fennmaradó rész a ledgerben marad a következő olyan időszakra, amelyben van menürendelés.
* **AC 7.1.5 (Értesítés):** A számla létrejöttekor a dolgozó értesítést kap a levont jóváírások részletezésével.
* **AC 7.1.6 (Időszakonként egy számla):** Egy felhasználóra egy időszakhoz legfeljebb egy számla keletkezik (unique `UserId` + `OrderingPeriodId`).

**Technikai hivatkozás:** `GeneratePeriodInvoicesCommand`, `PeriodInvoice`, `ICreditService`

---

### US-7.2: Számla kifizetettségének adminisztrátori jelölése `[A]`
**Leírás:**  
Mint **Rendszeradminisztrátor**,  
Akarok **egy időszaki számlát kézzel fizetettre állítani**,  
Azért, hogy nyilvántartsam a pénzügyi teljesítéseket.

**Elfogadási Kritériumok:**
* **AC 7.2.1:** Az adminisztrátor megjelölheti a számlát fizetettként (`IsPaid = true`), rögzítve a `PaidAtUtc` időpontot és a `MarkedPaidByUserId` azonosítót.

**Technikai hivatkozás:** `MarkInvoicePaidCommand`, `PeriodInvoice`

---

### US-7.3: Számlák adminisztrátori lekérdezése `[A]`
**Leírás:**  
Mint **Rendszeradminisztrátor**,  
Akarok **időszakra és fizetettségre szűrve listázni a számlákat**,  
Azért, hogy lássam, kitől van még hátralék.

**Elfogadási Kritériumok:**
* **AC 7.3.1 (Szűrők):** A lista szűrhető `OrderingPeriodId`-re és fizetettségi állapotra.
* **AC 7.3.2 (Bontás):** Minden soron látszik a bruttó menü, a bruttó a la carte, a beszámított jóváírás és a **két fizetendő sor** külön (menü / a la carte), valamint a végösszeg.

**Technikai hivatkozás:** `GetInvoicesQuery`

---

### US-7.4: Saját számláim megtekintése `[U]`
**Leírás:**  
Mint **Dolgozó**,  
Akarok **látni a saját időszaki számláimat**,  
Azért, hogy tudjam, mennyit kell fizetnem, és hogy a jóváírásaimat valóban beszámították-e.

**Elfogadási Kritériumok:**
* **AC 7.4.1 (Időszakonkénti bontás):** Időszakonként megjelenik a bruttó menü, a bruttó a la carte, a beszámított jóváírás és a két fizetendő sor.
* **AC 7.4.2 (Fizetettség):** A számla fizetettségi állapota és a fizetés időpontja látszik.
* **AC 7.4.3 (Kapcsolat a ledgerrel):** A beszámított jóváírás összege megegyezik a ledgerben az adott számlához (`PeriodInvoiceId`) tartozó `CreditApplied` tételek összegével (US-5.3).

**Technikai hivatkozás:** `GetMyInvoicesQuery`

---

## Epic 8: Értesítések és Tájékoztatás

### US-8.1: Rendszerértesítések kezelése `[U]`
**Leírás:**  
Mint **Dolgozó**,  
Akarok **in-app értesítéseket kapni a rendeléseimet érintő eseményekről és azokat olvasottnak jelölni**,  
Azért, hogy naprakész információval rendelkezzek a lemondásokról, átvezetésekről és jóváírásokról.

**Elfogadási Kritériumok:**
* **AC 8.1.1 (Értesítési típusok):** A rendszer automatikusan értesítést generál:
  - Menü módosulásakor (`MenuChanged`),
  - Variáns törlése miatti átvezetéskor (`OrderReassigned`),
  - Nap kizárása vagy menü törlése miatti lemondáskor (`MenuCancelled`),
  - Kizárás visszavonásakor (`OrderRestored` / `DayReopened`),
  - Jóváírás keletkezésekor és beszámításakor (`CreditIssued`, `CreditApplied`).
* **AC 8.1.2 (Olvasottság kezelése):** A dolgozó lekérheti az értesítéseit, és egyenként vagy egyszerre az összeset olvasottnak jelölheti (`ReadAtUtc` kitöltése).
* **AC 8.1.3 (Címzettség):** Ha a rendelést más adta le, az érintett esemény a rendelés tulajdonosához és a leadóhoz is eljut (AC 2.2.2).

**Technikai hivatkozás:** `GetMyNotificationsQuery`, `MarkNotificationReadCommand`, `MarkAllNotificationsReadCommand`, `UserNotification`

---

## Epic 9: Felhasználókezelés és Hozzáférés

> Ez az epic az architektúra 5. fejezetét (aktuális felhasználó, fejlesztői bejelentkezés) és a Users
> use case-eket fedi. A valódi vállalati hitelesítés bevezetésekor csak a `/dev-login` endpoint cserélődik
> — a szerepkör-kezelés és a többi story változatlan marad.

### US-9.1: Fejlesztői bejelentkezés és felhasználóváltás `[A/U]`
**Leírás:**  
Mint **Fejlesztő vagy tesztelő felhasználó**,  
Akarok **egy listából kiválasztott felhasználóként belépni és köztük váltani**,  
Azért, hogy a valódi hitelesítés bevezetése előtt is végig lehessen próbálni a felhasználói és admin folyamatokat.

**Elfogadási Kritériumok:**
* **AC 9.1.1 (Belépés):** A `POST /dev-login` végpont a megadott `userName` alapján megkeresi a felhasználót, és cookie authentication sütit állít be `NameIdentifier` (= `User.Id`), `Name` (= `UserName`) és `Role` claimekkel.
* **AC 9.1.2 (Form POST kötelező):** A váltó felület **form POST**-tal hívja a végpontot. Interaktív circuitből (`@onclick` + service hívás) nincs `HttpContext`, ezért a bejelentkezés úgy nem működik.
* **AC 9.1.3 (Tartósság):** A süti 30 napig érvényes, sliding expirationnel, és **túléli az alkalmazás újraindítását** — nem kell minden `dotnet run` után újra belépni.
* **AC 9.1.4 (Kilépés):** A `POST /dev-logout` érvényteleníti a munkamenetet.
* **AC 9.1.5 (Prerender és circuit):** Az aktuális felhasználó a prerender fázisban és az interaktív circuitben egyaránt ugyanazt az azonosítót adja.

**Technikai hivatkozás:** `DevAuthEndpoints`, `ICurrentUser`, `AddDevAuthentication()`

---

### US-9.2: Szerepkör-alapú hozzáférés `[A]`
**Leírás:**  
Mint **Rendszeradminisztrátor**,  
Akarok **hogy az adminisztrátori műveletek csak admin szerepkörrel legyenek elérhetők**,  
Azért, hogy egy dolgozó ne tudjon menüt szerkeszteni, napot kizárni vagy számlát generálni.

**Elfogadási Kritériumok:**
* **AC 9.2.1 (Policy):** Az `[A]` jelölésű use case-eket az `"Admin"` policy védi; `User` szerepkörrel a hívás elutasításra kerül.
* **AC 9.2.2 (Más nevében rendelés nem admin jog):** A más nevében történő rendelés (AC 3.1.6) **bárki** számára engedélyezett — ez szándékos döntés, a védelmet az audit (`PlacedByUserId`) adja, nem a jogosultság.
* **AC 9.2.3 (Saját adat hatóköre):** A `[U]` jelölésű lekérdezések (egyenleg, ledger, saját rendelések, saját számlák, értesítések) mindig a bejelentkezett felhasználó adatait adják vissza, más felhasználóét nem.

**Technikai hivatkozás:** `AddAuthorizationBuilder()`, `"Admin"` policy, `ICurrentUser`

---

### US-9.3: Elavult munkamenet érvénytelenítése `[A/U]`
**Leírás:**  
Mint **Fejlesztő**,  
Akarok **hogy a rendszer felismerje, ha a sütiben tárolt felhasználó már nem létezik**,  
Azért, hogy adatbázis-újragenerálás után ne maradjon érvényben egy „szellem" munkamenet.

**Elfogadási Kritériumok:**
* **AC 9.3.1:** Minden kérésnél ellenőrzésre kerül, hogy a sütiben tárolt `User.Id` létező felhasználóra mutat-e (`OnValidatePrincipal`).
* **AC 9.3.2:** Ha nem, a rendszer elutasítja a principalt és kijelentkeztet — a felhasználó a belépő felületre kerül, nem hibaoldalra.

**Technikai hivatkozás:** `OnValidatePrincipal`, `RejectPrincipal()`

---

### US-9.4: Felhasználók lekérdezése `[A/U]`
**Leírás:**  
Mint **Dolgozó vagy Rendszeradminisztrátor**,  
Akarok **lekérdezni a felhasználókat**,  
Azért, hogy legyen kit kiválasztani a felhasználóváltóban és a más nevében rendeléshez, az admin pedig lássa a teljes névsort.

**Elfogadási Kritériumok:**
* **AC 9.4.1 (Lista):** A felhasználók névvel (`VezetekNev`, `KeresztNev`), `UserName`-mel, céges `UserId`-vel és szerepkörrel kérhetők le, névsor szerint rendezve.
* **AC 9.4.2 (Egyedi lekérdezés):** Egy felhasználó azonosító és `UserName` alapján is lekérdezhető.
* **AC 9.4.3 (Felhasználásai):** Ugyanez a lista szolgálja ki a dev felhasználóváltót (US-9.1) és a más nevében rendelés címzett-választóját (AC 3.1.6).

**Technikai hivatkozás:** `GetUsersQuery`, `GetUserByIdQuery`, `GetUserByUserNameQuery`

---

## Epic 10: Kereszt-metsző követelmények

Ezek nem önálló story-k, hanem **minden** story-ra egyszerre érvényes követelmények. Az elfogadásnál
minden érintett use case-en ellenőrizendők.

### Nem funkcionális követelmények

* **NFR-1 (Időzóna):** Minden határidő (11:00 lemondási idő, 10:30 a la carte idő, `OrderDeadline`)
  **Europe/Budapest helyi időben** értelmezett; minden tárolt időpillanat UTC-ben megy az adatbázisba
  (`...AtUtc` utótag). Az idő kizárólag absztrakción keresztül olvasható (`IAppClock` / `TimeProvider`),
  hogy a határesetek (10:59 / 11:01) tesztelhetők legyenek.
* **NFR-2 (Hibakezelési konvenció):** A várt üzleti kimenetek (határidő lejárt, elfogyott, nap lezárva,
  időszakon kívüli nap) `Result` / `Result<T>` értékként térnek vissza, **nem kivételként**. Kivételt
  csak a bemeneti alakhelyességi hiba dob (`ValidationBehavior` + FluentValidation) — az valódi
  programhiba.
* **NFR-3 (Append-only ledger):** A `CreditEntry` sorok soha nem módosulnak és nem törlődnek; minden
  korrekció új, ellentétes előjelű tétel (`CreditRevoked`, `ManualAdjustment`). Kivétel a pozitív
  tételek `RemainingHuf` mezője, amely a felhasználást tartja karban.
* **NFR-4 (Audit kötelező):** Minden állapotváltó műveletnél rögzül, ki csinálta és mikor:
  `PlacedByUserId`, `CancelledByUserId`, `CreatedByUserId`, `ClosedByUserId`, `MarkedPaidByUserId`.
* **NFR-5 (Aznapi módosítás globális tilalma):** A 3 munkanapos szabályból következik, hogy aznapi
  menürendelés és aznapi lemondás **nem létezik** — ezt sem a felhasználó, sem az admin nem tudja
  kikerülni, és nincs is rá use case.
* **NFR-6 (Konkurencia):** Az időszak-átfedés ellenőrzése serializable tranzakcióban fut (párhuzamos
  felvitel nem csúszhat át); az a la carte készletfoglalás egyetlen atomi feltételes UPDATE, így
  párhuzamos rendelésnél sem lehet túlfoglalás. Egy handler = egy unit of work
  (`IDbContextFactory`-ból kért saját `DbContext`).
* **NFR-7 (Snapshot elv):** A pénzügyileg releváns adatok a rendelés pillanatában rögzülnek
  (`PriceHuf`, `ItemNameSnapshot`, `CategorySnapshot`, `UnitPriceHuf`, `OrderingPeriodId`), így a
  későbbi ár- vagy névváltozás nem írja át a múltat.

### Ismert korlátok

A `01-szerver-architektura.md` 11. fejezetéből azok a tételek, amelyek a felhasználói viselkedésben is
látszanak:

* **KL-1 (Nincs variánsváltás):** Nincs külön „A → B menüre váltok" use case; a dolgozó lemond és
  újrarendel, ami két ledger-tételt szül (jóváírás + új terhelés). Az összeg helyes, a kimutatás zajos.
* **KL-2 (Rés-napon nincs a la carte):** Lefedetlen napon az aznapi a la carte vásárlás is elutasításra
  kerül, mert nincs mire számlázni (AC 4.2.7).
* **KL-3 (Aznapi üzemzavar):** A rendszer nem kezeli az aznapi kiesést (áramszünet, konyhaleállás) —
  aznapi lemondás nincs, és a mai nap nem zárható ki. A kompenzáció kézi jóváírás (US-5.2).
* **KL-4 (Átfedés-védelem hatóköre):** Az időszak-átfedés tilalma csak alkalmazásszinten él; az
  adatbázison kívülről bevitt átfedő időszakot a rendszer nem veszi észre.
* **KL-5 (Korai napzárás):** A `CloseDayCommand` bármely jövőbeli napra kiadható, tehát az admin a
  3 munkanapos ablak lejárta előtt is elvághatja a rendelést és a lemondást. Ez szándékos rugalmasság,
  de nincs rá védőkorlát.
* **KL-6 (Kilépő dolgozó egyenlege):** A fel nem használt `RemainingHuf` kezelése nincs specifikálva;
  egyelőre az egyenlegen marad.

---

## Lefedettségi mátrix

Az `01-szerver-architektura.md` 6. fejezetének minden use case-e, és a lefedő user story.

| Use case | Szerep | User story |
|---|---|---|
| `GetUsersQuery` | A/U | US-9.4 |
| `GetUserByIdQuery` | A/U | US-9.4 |
| `GetUserByUserNameQuery` | A/U | US-9.4 |
| *dev auth (`/dev-login`, `/dev-logout`, `ICurrentUser`, Admin policy)* | A/U | US-9.1, US-9.2, US-9.3 |
| `UpsertOrderingPeriodCommand` | A | US-1.1 |
| `GetOrderingPeriodQuery` | A/U | US-1.4 |
| `GetOrderingPeriodsQuery` | A/U | US-1.4 |
| `GetOrderingPeriodForDateQuery` | A/U | US-1.4 |
| `GetUncoveredWorkdaysQuery` | A | US-1.5 |
| `ExcludeDayCommand` | A | US-1.2 |
| `RemoveExcludedDayCommand` | A | US-1.3 |
| `GetExcludedDaysQuery` | A/U | US-1.6 |
| `GetOrderableDaysQuery` | U | US-1.7 |
| `UpsertDailyMenuCommand` | A | US-2.1 (létrehozás), US-2.3 (módosítás) |
| `DeleteMenuVariantCommand` | A | US-2.2 |
| `DeleteDailyMenuCommand` | A | US-2.4 |
| `GetMenuDishSuggestionsQuery` | A | US-2.1 |
| `CreateMenuDishCommand` | A | US-2.1 |
| `UpdateMenuDishCommand` | A | US-2.1 |
| `GetDailyMenuQuery` | A/U | US-2.5 |
| `GetPeriodMenuQuery` | A/U | US-2.5 |
| `GetTodayMenuForUserQuery` | U | US-2.6 |
| `PlacePeriodOrderCommand` | U | US-3.1 |
| `CancelMenuOrdersCommand` | U | US-3.2 |
| `GetMyPeriodOrderQuery` | U | US-3.3 |
| `GetUserOrdersQuery` | A | US-3.4 |
| `UpsertALaCarteItemCommand` | A | US-4.3 |
| `DeactivateALaCarteItemCommand` | A | US-4.3 |
| `GetALaCarteItemsQuery` | A | US-4.3 |
| `SetDailyOfferCommand` | A | US-4.1 |
| `RemoveDailyOfferCommand` | A | US-4.4 |
| `GetDailyOffersQuery` | A/U | US-4.5 |
| `PlaceALaCarteOrderCommand` | U | US-4.2 |
| `GetALaCarteDailySummaryQuery` | A | US-4.6 |
| `GetALaCarteMonthlySummaryQuery` | A | US-4.6 |
| `GetKitchenSummaryQuery` | A | US-6.1 |
| `GetKitchenSummaryRangeQuery` | A | US-6.1 |
| `CloseDayCommand` | A | US-6.2 |
| `ReopenDayCommand` | A | US-6.2 |
| `GetKitchenClosureQuery` | A | US-6.3 |
| `GeneratePeriodInvoicesCommand` | A | US-7.1 |
| `MarkInvoicePaidCommand` | A | US-7.2 |
| `GetInvoicesQuery` | A | US-7.3 |
| `GetMyInvoicesQuery` | U | US-7.4 |
| `GetMyBalanceQuery` | U | US-5.1 |
| `GetMyCreditLedgerQuery` | U | US-5.3 |
| `AddManualCreditCommand` | A | US-5.2 |
| `GetMyNotificationsQuery` | U | US-8.1 |
| `MarkNotificationReadCommand` | U | US-8.1 |
| `MarkAllNotificationsReadCommand` | U | US-8.1 |

Minden use case-hez tartozik story és fordítva. A kereszt-metsző követelmények (Epic 10) minden sorra
vonatkoznak.

**Implementációs állapot:** az Epic 1–4 (Naptár/Menük/Rendelés/À la carte) teljes egészében
implementálva van. Az Epic 5–8 (Jóváírás, Konyha/napzárás, Számlázás, Értesítések — a fenti táblázat
`GetKitchenSummaryQuery`-től `MarkAllNotificationsReadCommand`-ig terjedő 15 sora) egyelőre csak
tervezve van, a kód még nem készült el hozzá (ld. `01-szerver-architektura.md` "10. Végrehajtási
sorrend", Fázis 6–7) — nincs se `Features/`, se UI, se `NavMenu` link ezekhez. Emiatt a jóváírás/
értesítés-írás (`ICreditService`/`INotificationService`, ami már ma is fut lemondás/kizárás/menütörlés
esetén) jelenleg write-only: a dolgozó felé nincs még olvasó oldal az egyenlegéhez vagy az
értesítéseihez.
