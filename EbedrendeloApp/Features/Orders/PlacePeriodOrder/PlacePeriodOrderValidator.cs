using FluentValidation;

namespace EbedrendeloApp.Features.Orders.PlacePeriodOrder;

public sealed class PlacePeriodOrderValidator : AbstractValidator<PlacePeriodOrderCommand>
{
    public PlacePeriodOrderValidator()
    {
        RuleFor(x => x.Days).NotEmpty();
        RuleFor(x => x.Days)
            .Must(days => days.Select(d => d.Date).Distinct().Count() == days.Count)
            .WithMessage("Egy dátum csak egyszer szerepelhet a listában.")
            .When(x => x.Days.Count > 0);

        RuleForEach(x => x.Days).ChildRules(day =>
        {
            day.RuleFor(d => d.VariantCode).NotEmpty();
        });
    }
}
