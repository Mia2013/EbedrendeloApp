namespace EbedrendeloApp.Data.Seed;

/// <summary>One seeded recipe with its nutrition/allergen info, in the same shape <see cref="Domain.Entities.MenuDish"/>
/// stores it — <see cref="Allergens"/> is a comma-separated selection from <see cref="Common.Allergens.AllergenCatalog"/>
/// (the fixed EU 1169/2011 Annex II numbering), not a site-specific scheme.</summary>
public sealed record SeedDish(string Name, decimal EnergyKcal, string? Allergens);

/// <summary>One seeded user. A <see cref="SzervKod"/> a szervezeti egységből képződik, nem külön adat —
/// lásd <see cref="SeedCatalog.Users"/>.</summary>
public sealed record SeedUser(
    int UserId,
    string UserName,
    string VezetekNev,
    string KeresztNev,
    string Igazgatosag,
    string Osztaly,
    string Rf,
    bool IsAdmin = false)
{
    public string SzervKod => $"{Igazgatosag}{Osztaly}";

    public string DisplayName => $"{VezetekNev} {KeresztNev}";
}

public static class SeedCatalog
{
    /// <summary>
    /// A fejlesztői felhasználók. Az igazgatóság/osztály szándékosan egy triviális séma — igazgatóság
    /// A–D, osztály 1–7 —, mert a más nevében rendeléshez a kollégát <b>pontosan</b> kell azonosítani
    /// (név + igazgatóság + osztály, lásd <c>ResolveColleagueHandler</c>), és valósághű, ékezetes
    /// egységneveket böngészőben körülményes begépelni. Élesben ezek az adatok a HR-rendszerből jönnek,
    /// üzleti logika nem épül rájuk.
    ///
    /// Két névütközés <b>szándékos</b>, hogy az azonosítás mindkét éles ága kézzel is kipróbálható legyen:
    /// <list type="bullet">
    /// <item>„Kovács János" kétszer, <b>különböző</b> egységben (A/1 és D/7) — az igazgatóság+osztály
    /// dönti el, melyikről van szó, tehát a hármas így is egyértelmű.</item>
    /// <item>„Tóth Eszter" kétszer, <b>ugyanabban</b> az egységben (B/3) — a hármas kétértelmű, a handler
    /// szándékosan elutasítja (nem tippel).</item>
    /// </list>
    ///
    /// A <c>UserId</c> → <c>UserName</c> párosítás szándékosan ugyanaz maradt, mint korábban: a
    /// <c>DatabaseSeeder</c> egy meglévő adatbázisban is ehhez a listához igazítja a sorokat, és a
    /// <c>UserName</c> egyedi indexes — egy „körbe-átnevezés" (A neve B-re, miközben B még használja) egy
    /// köteg-UPDATE közben ütközne. A két új név ezért friss <c>UserName</c>-et kapott, nem cserélt.
    /// </summary>
    public static readonly IReadOnlyList<SeedUser> Users =
    [
        new(1001, "admin", "Adminisztrátor", "Rendszer", "A", "1", "RF-000", IsAdmin: true),
        new(1002, "kovacs.j", "Kovács", "János", "A", "1", "RF-101"),
        new(1003, "nagy.a", "Nagy", "Anna", "A", "2", "RF-102"),
        new(1004, "szabo.p", "Szabó", "Péter", "A", "4", "RF-103"),
        new(1005, "toth.e", "Tóth", "Eszter", "B", "3", "RF-104"),
        new(1006, "varga.b", "Varga", "Balázs", "B", "4", "RF-105"),
        new(1007, "horvath.k", "Horváth", "Katalin", "C", "5", "RF-106"),
        new(1008, "kiss.z", "Kiss", "Zoltán", "C", "5", "RF-107"),
        new(1009, "molnar.r", "Molnár", "Réka", "C", "6", "RF-108"),
        new(1010, "kovacs.j2", "Kovács", "János", "D", "7", "RF-109"),
        new(1011, "papp.zs", "Papp", "Zsófia", "D", "7", "RF-110"),
        new(1012, "toth.e2", "Tóth", "Eszter", "B", "3", "RF-111"),
        new(1013, "szucs.n", "Szűcs", "Nóra", "A", "4", "RF-112"),
        new(1014, "juhasz.m", "Juhász", "Márton", "C", "6", "RF-113"),
    ];


    public static readonly IReadOnlyList<SeedDish> Soups =
    [
        new("Gulyásleves", 220, "1,9"),
        new("Csontleves", 90, "9"),
        new("Zöldségleves", 110, "9"),
        new("Bableves füstölt csülökkel", 340, "1,9,12"),
        new("Halászlé", 280, "4,9"),
        new("Tyúkhúsleves cérnametélttel", 240, "1,3,9"),
        new("Karfiolkrémleves", 190, "7,9"),
        new("Sárgaborsó-leves füstölt hússal", 310, "9,12"),
        new("Húsleves finomfőzelékkel", 230, "1,3,9"),
        new("Paradicsomleves", 150, "1,7,9"),
        new("Gombakrémleves", 210, "1,7,9"),
        new("Jókai bableves", 350, "1,9,12"),
        new("Sóska krémleves főtt tojással", 260, "1,3,7,9"),
        new("Erőleves cérnametélttel", 180, "1,3,9"),
        new("Meggyleves", 220, "7,12"),
        new("Brokkolikrémleves", 200, "7,9"),
    ];

    public static readonly IReadOnlyList<SeedDish> MainCourses =
    [
        new("Bécsi szelet rizibizivel", 680, "1,3,7"),
        new("Csirkepaprikás házi nokedlivel", 610, "1,3,7"),
        new("Töltött káposzta tejföllel", 590, "1,7,9,12"),
        new("Sertéspörkölt tarhonyával", 640, "1,3,9"),
        new("Rakott krumpli tejföllel", 560, "3,7"),
        new("Vadas marhahús galuskával", 620, "1,3,9,12"),
        new("Milánói makaróni reszelt sajttal", 580, "1,7"),
        new("Sertésborda steakburgonyával", 650, "12"),
        new("Grillcsirke friss vegyes salátával", 480, "10"),
        new("Sajtos-sonkás palacsinta", 540, "1,3,7"),
        new("Brassói aprópecsenye", 600, "9,10"),
        new("Bolognai spagetti", 630, "1,7,9"),
        new("Rántott csirkemell rizzsel", 670, "1,3,7"),
        new("Székelykáposzta tejföllel", 580, "7,9,12"),
        new("Zöldségfasírt burgonyapürével", 520, "1,3,7"),
        new("Rántott sajt rizzsel", 690, "1,3,7"),
        new("Cordon bleu rizibizivel", 700, "1,3,7"),
        new("Csirkecomb tepsiben sült burgonyával", 610, "10"),
        new("Pásztortarhonya", 640, "1,3,9"),
        new("Sertésszűz gombamártással", 590, "7,9"),
        new("Debreceni tokány tarhonyával", 630, "1,9,12"),
        new("Rántott máj petrezselymes burgonyával", 560, "1,3,7"),
        new("Halfilé rizzsel", 470, "4"),
        new("Zöldséges rizottó", 520, "7,9"),
        new("Currys csirke jázmin rizzsel", 610, "7,10"),
        new("Fűszeres csirkemell burgonyapürével", 540, "7"),
        new("Marharagu galuskával", 650, "1,3,9,12"),
        new("Sertéspecsenye párolt káposztával", 600, "9,12"),
    ];
}
