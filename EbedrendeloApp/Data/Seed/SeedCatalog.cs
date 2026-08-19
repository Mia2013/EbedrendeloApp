namespace EbedrendeloApp.Data.Seed;

public static class SeedCatalog
{
    public sealed record Recipe(string Name, string Description);

    public static readonly IReadOnlyList<Recipe> Recipes =
    [
        new("Gulyásleves", "Marhahúsból, burgonyával, csipetkével"),
        new("Rántott szelet", "Bécsi szelet rizibizivel"),
        new("Csirkepaprikás", "Házi nokedlivel"),
        new("Töltött káposzta", "Savanyú káposztával, tejföllel"),
        new("Vegetáriánus lecsó", "Tojással, friss kenyérrel"),
        new("Sertéspörkölt", "Tarhonyával"),
        new("Halászlé", "Pontyból, csípősen"),
        new("Zöldségleves", "Vegyes zöldségekkel, gazdagon"),
        new("Rakott krumpli", "Tojással, kolbásszal, tejföllel"),
        new("Bableves", "Füstölt csülökkel"),
        new("Vadas marhahús", "Házi galuskával"),
        new("Milánói makaróni", "Darált hússal, reszelt sajttal"),
        new("Sertésborda", "Steakburgonyával"),
        new("Grillcsirke saláta", "Friss vegyes salátával"),
        new("Sajtos-sonkás palacsinta", "Édes tejfölös öntettel"),
    ];
}
