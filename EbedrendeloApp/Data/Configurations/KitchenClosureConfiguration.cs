using EbedrendeloApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbedrendeloApp.Data.Configurations;

public sealed class KitchenClosureConfiguration : IEntityTypeConfiguration<KitchenClosure>
{
    public void Configure(EntityTypeBuilder<KitchenClosure> builder)
    {
        builder.HasIndex(k => k.Date).IsUnique();

        builder.HasOne<User>().WithMany().HasForeignKey(k => k.ClosedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(k => k.Lines)
            .WithOne(l => l.KitchenClosure)
            .HasForeignKey(l => l.KitchenClosureId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
