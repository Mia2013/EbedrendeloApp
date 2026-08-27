using EbedrendeloApp.Common.Allergens;

namespace EbedrendeloApp.Tests.Common.Allergens;

public class AllergenCatalogTests
{
    [Fact]
    public void Parse_reads_a_comma_separated_list_of_numbers()
    {
        var result = AllergenCatalog.Parse("1, 7,9");

        Assert.Equal([1, 7, 9], result.OrderBy(n => n));
    }

    [Fact]
    public void Parse_of_null_or_empty_returns_an_empty_set()
    {
        Assert.Empty(AllergenCatalog.Parse(null));
        Assert.Empty(AllergenCatalog.Parse(""));
        Assert.Empty(AllergenCatalog.Parse("   "));
    }

    [Fact]
    public void Serialize_sorts_and_deduplicates()
    {
        var result = AllergenCatalog.Serialize([9, 1, 9, 7]);

        Assert.Equal("1,7,9", result);
    }

    [Fact]
    public void Serialize_of_empty_collection_returns_null()
    {
        Assert.Null(AllergenCatalog.Serialize([]));
    }

    [Fact]
    public void Format_renders_number_dash_name_pairs_sorted_by_number()
    {
        var result = AllergenCatalog.Format("9,1");

        Assert.Equal("1 – Glutén, 9 – Zeller", result);
    }

    [Fact]
    public void Format_of_null_or_empty_returns_null()
    {
        Assert.Null(AllergenCatalog.Format(null));
        Assert.Null(AllergenCatalog.Format(""));
    }

    [Fact]
    public void FormatNumbersOnly_renders_just_the_sorted_numbers()
    {
        var result = AllergenCatalog.FormatNumbersOnly("9,1");

        Assert.Equal("1, 9", result);
    }

    [Fact]
    public void FormatNumbersOnly_of_null_or_empty_returns_null()
    {
        Assert.Null(AllergenCatalog.FormatNumbersOnly(null));
        Assert.Null(AllergenCatalog.FormatNumbersOnly(""));
    }

    [Fact]
    public void All_lists_exactly_the_14_EU_declared_allergens_numbered_1_to_14()
    {
        Assert.Equal(14, AllergenCatalog.All.Count);
        Assert.Equal(Enumerable.Range(1, 14), AllergenCatalog.All.Select(a => a.Number).OrderBy(n => n));
    }
}
