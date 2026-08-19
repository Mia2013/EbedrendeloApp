using EbedrendeloApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbedrendeloApp.Data.Configurations;

public sealed class ALaCarteOrderConfiguration : IEntityTypeConfiguration<ALaCarteOrder>
{
    public void Configure(EntityTypeBuilder<ALaCarteOrder> builder)
    {
        builder.HasIndex(o => new { o.UserId, o.Date }).IsUnique();

        builder.HasOne<User>().WithMany().HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(o => o.PlacedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrderingPeriod>().WithMany().HasForeignKey(o => o.OrderingPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Lines)
            .WithOne(l => l.ALaCarteOrder)
            .HasForeignKey(l => l.ALaCarteOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
