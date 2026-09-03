using FluentValidation;

namespace EbedrendeloApp.Features.ALaCarte.PlaceALaCarteOrder;

public sealed class PlaceALaCarteOrderValidator : AbstractValidator<PlaceALaCarteOrderCommand>
{
    public PlaceALaCarteOrderValidator()
    {
        RuleFor(x => x.ALaCarteItemIds).NotEmpty();
        RuleFor(x => x.ALaCarteItemIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Egy tétel csak egyszer szerepelhet a listában.")
            .When(x => x.ALaCarteItemIds.Count > 0);
    }
}
