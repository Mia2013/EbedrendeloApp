using EbedrendeloApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbedrendeloApp.Data.Configurations;

public sealed class KitchenClosureReopeningConfiguration : IEntityTypeConfiguration<KitchenClosureReopening>
{
    public void Configure(EntityTypeBuilder<KitchenClosureReopening> builder)
    {
        // 1:1 with KitchenClosure (see KitchenClosureConfiguration.HasOne(k => k.Reopening) for the
        // other side) — at most one reopen event per closure.
        builder.HasIndex(r => r.KitchenClosureId).IsUnique();

        builder.HasOne<User>().WithMany().HasForeignKey(r => r.ReopenedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
