using EbedrendeloApp.Common.Allergens;
using FluentValidation;

namespace EbedrendeloApp.Features.Menus.UpdateMenuDish;

public sealed class UpdateMenuDishValidator : AbstractValidator<UpdateMenuDishCommand>
{
    public UpdateMenuDishValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);

        RuleForEach(x => x.AllergenIds)
            .Must(id => AllergenCatalog.All.Any(a => a.Number == id))
            .WithMessage("Ismeretlen allergén azonosító.");

        RuleFor(x => x.EnergyKcal).GreaterThanOrEqualTo(0).When(x => x.EnergyKcal is not null);
        RuleFor(x => x.FatGrams).GreaterThanOrEqualTo(0).When(x => x.FatGrams is not null);
        RuleFor(x => x.SaturatedFatGrams).GreaterThanOrEqualTo(0).When(x => x.SaturatedFatGrams is not null);
        RuleFor(x => x.CarbohydrateGrams).GreaterThanOrEqualTo(0).When(x => x.CarbohydrateGrams is not null);
        RuleFor(x => x.SugarGrams).GreaterThanOrEqualTo(0).When(x => x.SugarGrams is not null);
        RuleFor(x => x.ProteinGrams).GreaterThanOrEqualTo(0).When(x => x.ProteinGrams is not null);
        RuleFor(x => x.SaltGrams).GreaterThanOrEqualTo(0).When(x => x.SaltGrams is not null);
    }
}
