using FluentValidation;

namespace EbedrendeloApp.Features.Billing.AddManualCredit;

public sealed class AddManualCreditValidator : AbstractValidator<AddManualCreditCommand>
{
    public AddManualCreditValidator()
    {
        RuleFor(x => x.TargetUserId).GreaterThan(0);
        RuleFor(x => x.AmountHuf).GreaterThan(0).WithMessage("Az összegnek pozitívnak kell lennie.");

        // MaximumLength matches CreditEntryConfiguration's Note column (nvarchar(500)) — without it, an
        // over-length note passes validation and throws a raw SQL truncation error at SaveChangesAsync
        // instead of a friendly Result.Failure.
        RuleFor(x => x.Note).NotEmpty().WithMessage("Az indoklás megadása kötelező.").MaximumLength(500);
    }
}
