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
            variant.RuleFor(v => v.SoupAllergens).MaximumLength(300);
            variant.RuleFor(v => v.MainCourseAllergens).MaximumLength(300);

            variant.RuleFor(v => v.SoupEnergyKcal).GreaterThanOrEqualTo(0).When(v => v.SoupEnergyKcal is not null);
            variant.RuleFor(v => v.SoupFatGrams).GreaterThanOrEqualTo(0).When(v => v.SoupFatGrams is not null);
            variant.RuleFor(v => v.SoupSaturatedFatGrams).GreaterThanOrEqualTo(0).When(v => v.SoupSaturatedFatGrams is not null);
            variant.RuleFor(v => v.SoupCarbohydrateGrams).GreaterThanOrEqualTo(0).When(v => v.SoupCarbohydrateGrams is not null);
            variant.RuleFor(v => v.SoupSugarGrams).GreaterThanOrEqualTo(0).When(v => v.SoupSugarGrams is not null);
            variant.RuleFor(v => v.SoupProteinGrams).GreaterThanOrEqualTo(0).When(v => v.SoupProteinGrams is not null);
            variant.RuleFor(v => v.SoupSaltGrams).GreaterThanOrEqualTo(0).When(v => v.SoupSaltGrams is not null);

            variant.RuleFor(v => v.MainCourseEnergyKcal).GreaterThanOrEqualTo(0).When(v => v.MainCourseEnergyKcal is not null);
            variant.RuleFor(v => v.MainCourseFatGrams).GreaterThanOrEqualTo(0).When(v => v.MainCourseFatGrams is not null);
            variant.RuleFor(v => v.MainCourseSaturatedFatGrams).GreaterThanOrEqualTo(0).When(v => v.MainCourseSaturatedFatGrams is not null);
            variant.RuleFor(v => v.MainCourseCarbohydrateGrams).GreaterThanOrEqualTo(0).When(v => v.MainCourseCarbohydrateGrams is not null);
            variant.RuleFor(v => v.MainCourseSugarGrams).GreaterThanOrEqualTo(0).When(v => v.MainCourseSugarGrams is not null);
            variant.RuleFor(v => v.MainCourseProteinGrams).GreaterThanOrEqualTo(0).When(v => v.MainCourseProteinGrams is not null);
            variant.RuleFor(v => v.MainCourseSaltGrams).GreaterThanOrEqualTo(0).When(v => v.MainCourseSaltGrams is not null);
        });

        RuleFor(x => x.Variants)
            .Must(variants => variants.Select(v => v.Code).Distinct(StringComparer.Ordinal).Count() == variants.Count)
            .WithMessage("A variánskódok nem ismétlődhetnek.")
            .When(x => x.Variants.Count > 0);
    }
}
