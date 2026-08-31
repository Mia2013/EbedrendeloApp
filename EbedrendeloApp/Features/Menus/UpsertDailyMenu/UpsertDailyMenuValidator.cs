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
            // A variánskód zárt halmaz (A/B/C) — a megjelenítési szöveg ("A menü" stb.) mindig a UI
            // dolga, a Code önmagában sosem tartalmazhatja azt.
            variant.RuleFor(v => v.Code).Must(code => code is "A" or "B" or "C")
                .WithMessage("A variánskód csak A, B vagy C lehet.");
            variant.RuleFor(v => v.SoupDishId).GreaterThan(0).WithMessage("Válassz levest a katalógusból.");
            variant.RuleFor(v => v.SoupAllergens).MaximumLength(300);
            variant.RuleFor(v => v.MainCourseAllergens).MaximumLength(300);

            // Upper bound matches MenuDishConfiguration's decimal(6,2) column precision (max 9999.99) —
            // without it, a typo'd or unit-mismatched value (e.g. kJ entered instead of kcal) passes
            // validation here and then throws a raw SQL arithmetic-overflow exception at SaveChangesAsync.
            variant.RuleFor(v => v.SoupEnergyKcal).InclusiveBetween(0, 9999.99m).When(v => v.SoupEnergyKcal is not null);
            variant.RuleFor(v => v.SoupFatGrams).InclusiveBetween(0, 9999.99m).When(v => v.SoupFatGrams is not null);
            variant.RuleFor(v => v.SoupSaturatedFatGrams).InclusiveBetween(0, 9999.99m).When(v => v.SoupSaturatedFatGrams is not null);
            variant.RuleFor(v => v.SoupCarbohydrateGrams).InclusiveBetween(0, 9999.99m).When(v => v.SoupCarbohydrateGrams is not null);
            variant.RuleFor(v => v.SoupSugarGrams).InclusiveBetween(0, 9999.99m).When(v => v.SoupSugarGrams is not null);
            variant.RuleFor(v => v.SoupProteinGrams).InclusiveBetween(0, 9999.99m).When(v => v.SoupProteinGrams is not null);
            variant.RuleFor(v => v.SoupSaltGrams).InclusiveBetween(0, 9999.99m).When(v => v.SoupSaltGrams is not null);

            variant.RuleFor(v => v.MainCourseEnergyKcal).InclusiveBetween(0, 9999.99m).When(v => v.MainCourseEnergyKcal is not null);
            variant.RuleFor(v => v.MainCourseFatGrams).InclusiveBetween(0, 9999.99m).When(v => v.MainCourseFatGrams is not null);
            variant.RuleFor(v => v.MainCourseSaturatedFatGrams).InclusiveBetween(0, 9999.99m).When(v => v.MainCourseSaturatedFatGrams is not null);
            variant.RuleFor(v => v.MainCourseCarbohydrateGrams).InclusiveBetween(0, 9999.99m).When(v => v.MainCourseCarbohydrateGrams is not null);
            variant.RuleFor(v => v.MainCourseSugarGrams).InclusiveBetween(0, 9999.99m).When(v => v.MainCourseSugarGrams is not null);
            variant.RuleFor(v => v.MainCourseProteinGrams).InclusiveBetween(0, 9999.99m).When(v => v.MainCourseProteinGrams is not null);
            variant.RuleFor(v => v.MainCourseSaltGrams).InclusiveBetween(0, 9999.99m).When(v => v.MainCourseSaltGrams is not null);
        });

        RuleFor(x => x.Variants)
            .Must(variants => variants.Select(v => v.Code).Distinct(StringComparer.Ordinal).Count() == variants.Count)
            .WithMessage("A variánskódok nem ismétlődhetnek.")
            .When(x => x.Variants.Count > 0);
    }
}
