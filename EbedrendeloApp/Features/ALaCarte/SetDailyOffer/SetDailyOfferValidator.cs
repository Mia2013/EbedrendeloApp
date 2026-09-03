using FluentValidation;

namespace EbedrendeloApp.Features.ALaCarte.SetDailyOffer;

public sealed class SetDailyOfferValidator : AbstractValidator<SetDailyOfferCommand>
{
    public SetDailyOfferValidator()
    {
        RuleFor(x => x.Capacity).GreaterThanOrEqualTo(0);
    }
}
