using FluentValidation;

namespace EbedrendeloApp.Features.Billing.MarkInvoicePaid;

public sealed class MarkInvoicePaidValidator : AbstractValidator<MarkInvoicePaidCommand>
{
    public MarkInvoicePaidValidator()
    {
        RuleFor(x => x.InvoiceId).GreaterThan(0);
        RuleFor(x => x.MarkedByUserId).GreaterThan(0);
    }
}
