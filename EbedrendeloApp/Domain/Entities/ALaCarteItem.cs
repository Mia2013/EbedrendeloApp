using EbedrendeloApp.Domain.Enums;

namespace EbedrendeloApp.Domain.Entities;

public sealed class ALaCarteItem
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required ALaCarteCategory Category { get; set; }
    public required int PriceHuf { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Allergens { get; set; }
    public decimal? EnergyKcal { get; set; }
    public decimal? FatGrams { get; set; }
    public decimal? SaturatedFatGrams { get; set; }
    public decimal? CarbohydrateGrams { get; set; }
    public decimal? SugarGrams { get; set; }
    public decimal? ProteinGrams { get; set; }
    public decimal? SaltGrams { get; set; }
}
