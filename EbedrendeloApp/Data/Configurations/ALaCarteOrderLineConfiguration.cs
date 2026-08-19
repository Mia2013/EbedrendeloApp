using EbedrendeloApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbedrendeloApp.Data.Configurations;

public sealed class ALaCarteOrderLineConfiguration : IEntityTypeConfiguration<ALaCarteOrderLine>
{
    public void Configure(EntityTypeBuilder<ALaCarteOrderLine> builder)
    {
        builder.HasIndex(l => new { l.ALaCarteOrderId, l.ALaCarteDailyOfferId }).IsUnique();
        builder.Property(l => l.ItemNameSnapshot).HasMaxLength(128);

        builder.HasOne<ALaCarteDailyOffer>().WithMany().HasForeignKey(l => l.ALaCarteDailyOfferId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
