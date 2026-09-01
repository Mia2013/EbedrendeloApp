using EbedrendeloApp.Common.Allergens;
using FluentValidation;

namespace EbedrendeloApp.Features.ALaCarte.UpsertALaCarteItem;

public sealed class UpsertALaCarteItemValidator : AbstractValidator<UpsertALaCarteItemCommand>
{
    public UpsertALaCarteItemValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.PriceHuf).GreaterThanOrEqualTo(0);

        RuleForEach(x => x.AllergenIds)
            .Must(id => AllergenCatalog.All.Any(a => a.Number == id))
            .WithMessage("Ismeretlen allergén azonosító.");

        // Upper bound matches ALaCarteItemConfiguration's decimal(6,2) column precision (max 9999.99) —
        // without it, an out-of-range value passes validation and throws a raw SQL arithmetic-overflow
        // exception at SaveChangesAsync instead of a friendly Result.Failure.
        RuleFor(x => x.EnergyKcal).InclusiveBetween(0, 9999.99m).When(x => x.EnergyKcal is not null);
        RuleFor(x => x.FatGrams).InclusiveBetween(0, 9999.99m).When(x => x.FatGrams is not null);
        RuleFor(x => x.SaturatedFatGrams).InclusiveBetween(0, 9999.99m).When(x => x.SaturatedFatGrams is not null);
        RuleFor(x => x.CarbohydrateGrams).InclusiveBetween(0, 9999.99m).When(x => x.CarbohydrateGrams is not null);
        RuleFor(x => x.SugarGrams).InclusiveBetween(0, 9999.99m).When(x => x.SugarGrams is not null);
        RuleFor(x => x.ProteinGrams).InclusiveBetween(0, 9999.99m).When(x => x.ProteinGrams is not null);
        RuleFor(x => x.SaltGrams).InclusiveBetween(0, 9999.99m).When(x => x.SaltGrams is not null);
    }
}
