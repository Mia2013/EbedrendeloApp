using EbedrendeloApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbedrendeloApp.Data.Configurations;

public sealed class DailyMenuConfiguration : IEntityTypeConfiguration<DailyMenu>
{
    public void Configure(EntityTypeBuilder<DailyMenu> builder)
    {
        builder.HasIndex(m => m.Date).IsUnique();
        builder.Property(m => m.Note).HasMaxLength(500);

        builder.HasMany(m => m.Variants)
            .WithOne(v => v.DailyMenu)
            .HasForeignKey(v => v.DailyMenuId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
