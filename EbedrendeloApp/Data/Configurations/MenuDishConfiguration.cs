using EbedrendeloApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbedrendeloApp.Data.Configurations;

public sealed class MenuDishConfiguration : IEntityTypeConfiguration<MenuDish>
{
    public void Configure(EntityTypeBuilder<MenuDish> builder)
    {
        builder.HasIndex(d => new { d.Kind, d.Name }).IsUnique();

        builder.Property(d => d.Name).HasMaxLength(128);
        builder.Property(d => d.Allergens).HasMaxLength(300);

        builder.Property(d => d.EnergyKcal).HasPrecision(6, 2);
        builder.Property(d => d.FatGrams).HasPrecision(6, 2);
        builder.Property(d => d.SaturatedFatGrams).HasPrecision(6, 2);
        builder.Property(d => d.CarbohydrateGrams).HasPrecision(6, 2);
        builder.Property(d => d.SugarGrams).HasPrecision(6, 2);
        builder.Property(d => d.ProteinGrams).HasPrecision(6, 2);
        builder.Property(d => d.SaltGrams).HasPrecision(6, 2);
    }
}
