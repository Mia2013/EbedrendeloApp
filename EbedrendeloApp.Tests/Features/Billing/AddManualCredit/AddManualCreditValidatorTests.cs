using EbedrendeloApp.Features.Billing.AddManualCredit;

namespace EbedrendeloApp.Tests.Features.Billing.AddManualCredit;

public class AddManualCreditValidatorTests
{
    [Fact]
    public void Rejects_an_empty_note()
    {
        var result = new AddManualCreditValidator().Validate(new AddManualCreditCommand(1, 500, "", 2));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Rejects_a_zero_or_negative_amount(int amountHuf)
    {
        var result = new AddManualCreditValidator().Validate(new AddManualCreditCommand(1, amountHuf, "Indoklás", 2));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Accepts_a_positive_amount_with_a_note()
    {
        var result = new AddManualCreditValidator().Validate(new AddManualCreditCommand(1, 500, "Indoklás", 2));

        Assert.True(result.IsValid);
    }
}
