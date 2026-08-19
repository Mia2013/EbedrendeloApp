using EbedrendeloApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbedrendeloApp.Data.Configurations;

public sealed class MenuOrderConfiguration : IEntityTypeConfiguration<MenuOrder>
{
    public void Configure(EntityTypeBuilder<MenuOrder> builder)
    {
        builder.HasIndex(o => new { o.UserId, o.Date })
            .IsUnique()
            .HasFilter("[Status] = 0");
        builder.HasIndex(o => new { o.Date, o.Status });
        builder.HasIndex(o => new { o.OrderingPeriodId, o.UserId });

        builder.Property(o => o.ReassignedFromVariantCode).HasMaxLength(8);

        builder.HasOne<User>().WithMany().HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(o => o.PlacedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(o => o.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrderingPeriod>().WithMany().HasForeignKey(o => o.OrderingPeriodId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MenuVariant>().WithMany().HasForeignKey(o => o.MenuVariantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExcludedDay>().WithMany().HasForeignKey(o => o.CancelledByExcludedDayId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
