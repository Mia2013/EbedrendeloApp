using FluentValidation;

namespace EbedrendeloApp.Features.Calendar.UpsertOrderingPeriod;

public sealed class UpsertOrderingPeriodValidator : AbstractValidator<UpsertOrderingPeriodCommand>
{
    public UpsertOrderingPeriodValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(64);
        RuleFor(x => x.StartDate).LessThanOrEqualTo(x => x.EndDate)
            .WithMessage("A kezdés dátuma nem lehet későbbi a zárás dátumánál.");
    }
}
