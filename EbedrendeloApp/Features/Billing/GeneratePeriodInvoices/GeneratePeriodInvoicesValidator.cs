using FluentValidation;

namespace EbedrendeloApp.Features.Billing.GeneratePeriodInvoices;

public sealed class GeneratePeriodInvoicesValidator : AbstractValidator<GeneratePeriodInvoicesCommand>
{
    public GeneratePeriodInvoicesValidator()
    {
        RuleFor(x => x.OrderingPeriodId).GreaterThan(0);
        RuleFor(x => x.GeneratedByUserId).GreaterThan(0);
    }
}
