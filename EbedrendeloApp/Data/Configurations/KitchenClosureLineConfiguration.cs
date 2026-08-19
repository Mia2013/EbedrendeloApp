using EbedrendeloApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbedrendeloApp.Data.Configurations;

public sealed class KitchenClosureLineConfiguration : IEntityTypeConfiguration<KitchenClosureLine>
{
    public void Configure(EntityTypeBuilder<KitchenClosureLine> builder)
    {
        builder.Property(l => l.VariantCode).HasMaxLength(8);
        builder.Property(l => l.VariantNameSnapshot).HasMaxLength(128);
    }
}
