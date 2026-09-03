using EbedrendeloApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbedrendeloApp.Data.Configurations;

public sealed class ALaCarteItemConfiguration : IEntityTypeConfiguration<ALaCarteItem>
{
    public void Configure(EntityTypeBuilder<ALaCarteItem> builder)
    {
        builder.HasIndex(i => new { i.Category, i.Name }).IsUnique();

        builder.Property(i => i.Name).HasMaxLength(128);
        builder.Property(i => i.Allergens).HasMaxLength(300);

        builder.Property(i => i.EnergyKcal).HasPrecision(6, 2);
        builder.Property(i => i.FatGrams).HasPrecision(6, 2);
        builder.Property(i => i.SaturatedFatGrams).HasPrecision(6, 2);
        builder.Property(i => i.CarbohydrateGrams).HasPrecision(6, 2);
        builder.Property(i => i.SugarGrams).HasPrecision(6, 2);
        builder.Property(i => i.ProteinGrams).HasPrecision(6, 2);
        builder.Property(i => i.SaltGrams).HasPrecision(6, 2);
    }
}
