using EbedrendeloApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbedrendeloApp.Data.Configurations;

public sealed class MenuVariantConfiguration : IEntityTypeConfiguration<MenuVariant>
{
    public void Configure(EntityTypeBuilder<MenuVariant> builder)
    {
        builder.HasIndex(v => new { v.DailyMenuId, v.Code }).IsUnique();

        builder.Property(v => v.Code).HasMaxLength(8);
        builder.Property(v => v.SoupName).HasMaxLength(128);
        builder.Property(v => v.MainCourseName).HasMaxLength(500);

        builder.HasOne(v => v.SoupDish).WithMany().HasForeignKey(v => v.SoupDishId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(v => v.MainCourseDish).WithMany().HasForeignKey(v => v.MainCourseDishId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
