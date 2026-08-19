using EbedrendeloApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbedrendeloApp.Data.Configurations;

public sealed class ALaCarteDailyOfferConfiguration : IEntityTypeConfiguration<ALaCarteDailyOffer>
{
    public void Configure(EntityTypeBuilder<ALaCarteDailyOffer> builder)
    {
        builder.HasIndex(o => new { o.Date, o.ALaCarteItemId }).IsUnique();

        builder.HasOne(o => o.ALaCarteItem)
            .WithMany()
            .HasForeignKey(o => o.ALaCarteItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
