namespace EbedrendeloApp.Data.Seed;

/// <summary>One seeded recipe with its nutrition/allergen info, in the same shape <see cref="Domain.Entities.MenuDish"/>
/// stores it — <see cref="Allergens"/> is a comma-separated selection from <see cref="Common.Allergens.AllergenCatalog"/>
/// (the fixed EU 1169/2011 Annex II numbering), not a site-specific scheme.</summary>
public sealed record SeedDish(string Name, decimal EnergyKcal, string? Allergens);

public static class SeedCatalog
{
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
