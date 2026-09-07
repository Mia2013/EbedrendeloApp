using EbedrendeloApp.Common.Billing;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Billing.GetMyCreditLedger;

namespace EbedrendeloApp.Tests.Common.Billing;

public class CreditEntryDisplayTests
{
    private static CreditLedgerEntryDto Entry(
        CreditEntryKind kind = CreditEntryKind.ManualAdjustment,
        string? note = null,
        DateOnly? sourceOrderDate = null,
        string? sourceOrderVariantCode = null) =>
        new(1, kind, 500, 500, DateTime.UtcNow, note, 2, "Nagy Éva", null, sourceOrderDate, sourceOrderVariantCode, null, null);

    [Theory]
    [InlineData(CreditEntryKind.CancellationCredit, "Lemondási jóváírás")]
    [InlineData(CreditEntryKind.CreditApplied, "Beszámítás")]
    [InlineData(CreditEntryKind.CreditRevoked, "Visszavonás")]
    [InlineData(CreditEntryKind.ManualAdjustment, "Kézi korrekció")]
    public void KindLabel_returns_hungarian_text_for_each_kind(CreditEntryKind kind, string expected)
    {
        Assert.Equal(expected, CreditEntryDisplay.KindLabel(kind));
    }

    [Fact]
    public void Describe_shows_the_order_date_and_variant_code_for_a_cancellation()
    {
        var entry = Entry(
            kind: CreditEntryKind.CancellationCredit,
            note: null,
            sourceOrderDate: new DateOnly(2026, 8, 20),
            sourceOrderVariantCode: "A");

        Assert.Equal("2026.08.20. A menü", CreditEntryDisplay.Describe(entry));
    }

    [Fact]
    public void Describe_falls_back_to_kind_label_when_no_order_and_no_note()
    {
        var entry = Entry(kind: CreditEntryKind.ManualAdjustment, note: null);

        Assert.Equal("Kézi korrekció (Nagy Éva)", CreditEntryDisplay.Describe(entry));
    }

    [Fact]
    public void Describe_omits_variant_suffix_when_variant_code_is_null()
    {
        var entry = Entry(sourceOrderDate: new DateOnly(2026, 8, 20), sourceOrderVariantCode: null);

        Assert.Equal("2026.08.20. menü", CreditEntryDisplay.Describe(entry));
    }

    [Fact]
    public void Describe_appends_the_note_when_present()
    {
        var entry = Entry(note: "Konyhai üzemzavar kompenzációja");

        Assert.Equal("Konyhai üzemzavar kompenzációja (Nagy Éva)", CreditEntryDisplay.Describe(entry));
    }
}
