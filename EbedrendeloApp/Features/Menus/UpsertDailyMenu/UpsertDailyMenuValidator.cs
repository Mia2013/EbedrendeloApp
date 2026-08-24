using FluentValidation;

namespace EbedrendeloApp.Features.Menus.UpsertDailyMenu;

public sealed class UpsertDailyMenuValidator : AbstractValidator<UpsertDailyMenuCommand>
{
    public UpsertDailyMenuValidator()
    {
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.Variants).NotEmpty().WithMessage("Legalább egy menüvariáns szükséges.");

        RuleForEach(x => x.Variants).ChildRules(variant =>
        {
            variant.RuleFor(v => v.Code).NotEmpty().MaximumLength(8);
            variant.RuleFor(v => v.Name).NotEmpty().MaximumLength(128);
            variant.RuleFor(v => v.Description).MaximumLength(500);
        });

        RuleFor(x => x.Variants)
            .Must(variants => variants.Select(v => v.Code).Distinct(StringComparer.Ordinal).Count() == variants.Count)
            .WithMessage("A variánskódok nem ismétlődhetnek.")
            .When(x => x.Variants.Count > 0);
    }
}
