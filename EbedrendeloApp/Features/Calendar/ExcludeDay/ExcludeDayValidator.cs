using FluentValidation;

namespace EbedrendeloApp.Features.Calendar.ExcludeDay;

public sealed class ExcludeDayValidator : AbstractValidator<ExcludeDayCommand>
{
    public ExcludeDayValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(200);
    }
}
