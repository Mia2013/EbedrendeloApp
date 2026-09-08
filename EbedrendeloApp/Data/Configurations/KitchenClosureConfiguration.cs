using EbedrendeloApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbedrendeloApp.Data.Configurations;

public sealed class KitchenClosureConfiguration : IEntityTypeConfiguration<KitchenClosure>
{
    public void Configure(EntityTypeBuilder<KitchenClosure> builder)
    {
        // Not unique: a date can be closed more than once over its lifetime (close → reopen → close
        // again), and KitchenClosure is append-only — every close inserts a new row rather than
        // updating an old one. See Common/Services/KitchenClosureQueries.cs for the "currently closed"
        // query this index supports.
        builder.HasIndex(k => k.Date);

        builder.HasOne<User>().WithMany().HasForeignKey(k => k.ClosedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(k => k.Lines)
            .WithOne(l => l.KitchenClosure)
            .HasForeignKey(l => l.KitchenClosureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(k => k.Reopening)
            .WithOne(r => r.KitchenClosure)
            .HasForeignKey<KitchenClosureReopening>(r => r.KitchenClosureId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
