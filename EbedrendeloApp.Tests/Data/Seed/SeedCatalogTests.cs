using EbedrendeloApp.Data.Seed;

namespace EbedrendeloApp.Tests.Data.Seed;

/// <summary>
/// A demoadat <i>szándékát</i> rögzíti, nem a konkrét neveket: a kolléga-azonosítás (név + igazgatóság +
/// osztály) csak akkor próbálható ki böngészőben, ha a seed tartalmazza mind a négy igazgatóságot, mind a
/// hét osztályt, és mindkét névütközés-esetet. Ezek DB nélkül, ezredmásodperc alatt futnak.
/// </summary>
public class SeedCatalogTests
{
    private static readonly string[] ExpectedIgazgatosagok = ["A", "B", "C", "D"];
    private static readonly string[] ExpectedOsztalyok = ["1", "2", "3", "4", "5", "6", "7"];

    [Fact]
    public void Every_seeded_user_has_an_organisational_unit()
    {
        // Az admin sem kivétel — a kolléga-keresésben ő is megtalálható kell legyen.
        Assert.All(SeedCatalog.Users, user =>
        {
            Assert.False(string.IsNullOrWhiteSpace(user.Igazgatosag));
            Assert.False(string.IsNullOrWhiteSpace(user.Osztaly));
        });
    }

    [Fact]
    public void User_ids_and_user_names_are_unique()
    {
        // A UserName egyedi indexes; egy ütköző katalógus csak futásidőben, a seedelésnél derülne ki.
        Assert.Equal(SeedCatalog.Users.Count, SeedCatalog.Users.Select(u => u.UserId).Distinct().Count());
        Assert.Equal(SeedCatalog.Users.Count, SeedCatalog.Users.Select(u => u.UserName).Distinct().Count());
    }

    [Fact]
    public void There_is_exactly_one_admin()
    {
        Assert.Single(SeedCatalog.Users, u => u.IsAdmin);
    }

    [Fact]
    public void All_four_directorates_and_all_seven_departments_are_represented()
    {
        Assert.Equal(ExpectedIgazgatosagok, SeedCatalog.Users.Select(u => u.Igazgatosag).Distinct().Order());
        Assert.Equal(ExpectedOsztalyok, SeedCatalog.Users.Select(u => u.Osztaly).Distinct().Order());
    }

    [Fact]
    public void Szervkod_is_derived_from_the_unit()
    {
        Assert.All(SeedCatalog.Users, user => Assert.Equal($"{user.Igazgatosag}{user.Osztaly}", user.SzervKod));
    }

    [Fact]
    public void Exactly_one_name_repeats_across_different_units_so_the_unit_disambiguates_it()
    {
        // ResolveColleagueHandler a hármasra keres: azonos név két különböző egységben továbbra is
        // egyértelműen azonosítható. Ez a kézi próba „sikeres" ága.
        var acrossUnits = SeedCatalog.Users
            .GroupBy(u => u.DisplayName)
            .Where(g => g.Count() > 1 && g.DistinctBy(u => (u.Igazgatosag, u.Osztaly)).Count() == g.Count())
            .ToList();

        var group = Assert.Single(acrossUnits);
        Assert.Equal(2, group.Count());
    }

    [Fact]
    public void Exactly_one_triple_is_ambiguous_so_the_rejecting_branch_can_be_tried_by_hand()
    {
        // Azonos név AZONOS egységben: a handler matches.Count != 1 ága szándékosan elutasít, nem tippel.
        var ambiguous = SeedCatalog.Users
            .GroupBy(u => (u.DisplayName, u.Igazgatosag, u.Osztaly))
            .Where(g => g.Count() > 1)
            .ToList();

        var group = Assert.Single(ambiguous);
        Assert.Equal(2, group.Count());
    }
}
