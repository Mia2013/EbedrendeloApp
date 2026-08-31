using FluentValidation;

namespace EbedrendeloApp.Features.Orders.CancelMenuOrders;

public sealed class CancelMenuOrdersValidator : AbstractValidator<CancelMenuOrdersCommand>
{
    public CancelMenuOrdersValidator()
    {
        RuleFor(x => x.Dates).NotEmpty();
    }
}
