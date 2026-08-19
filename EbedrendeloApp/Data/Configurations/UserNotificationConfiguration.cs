using EbedrendeloApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbedrendeloApp.Data.Configurations;

public sealed class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.HasIndex(n => new { n.UserId, n.ReadAtUtc, n.CreatedAtUtc });

        builder.Property(n => n.Title).HasMaxLength(128);
        builder.Property(n => n.Message).HasMaxLength(1000);

        builder.HasOne<User>().WithMany().HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MenuOrder>().WithMany().HasForeignKey(n => n.RelatedMenuOrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
