using EbedrendeloApp.Features.Orders;

namespace EbedrendeloApp.Tests.Features.Orders;

public class PendingOrderSummaryTests
{
    [Fact]
    public void Groups_by_variant_code_and_computes_the_price_per_group()
    {
        var summary = PendingOrderSummary.Summarize(["A", "B", "A"], menuPortionHuf: 1400);

        Assert.Collection(summary,
            line =>
            {
                Assert.Equal("A", line.VariantCode);
                Assert.Equal(2, line.Count);
                Assert.Equal(2800, line.TotalHuf);
            },
            line =>
            {
                Assert.Equal("B", line.VariantCode);
                Assert.Equal(1, line.Count);
                Assert.Equal(1400, line.TotalHuf);
            });
    }

    [Fact]
    public void Returns_an_empty_list_for_no_selections()
    {
        var summary = PendingOrderSummary.Summarize([], menuPortionHuf: 1400);

        Assert.Empty(summary);
    }

    [Fact]
    public void Orders_lines_by_variant_code_regardless_of_selection_order()
    {
        var summary = PendingOrderSummary.Summarize(["C", "A", "B"], menuPortionHuf: 1000);

        Assert.Equal(["A", "B", "C"], summary.Select(l => l.VariantCode));
    }
}
