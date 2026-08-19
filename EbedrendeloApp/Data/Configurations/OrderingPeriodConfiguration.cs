using EbedrendeloApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbedrendeloApp.Data.Configurations;

public sealed class OrderingPeriodConfiguration : IEntityTypeConfiguration<OrderingPeriod>
{
    public void Configure(EntityTypeBuilder<OrderingPeriod> builder)
    {
        builder.HasIndex(p => p.StartDate).IsUnique();
        builder.HasIndex(p => p.EndDate).IsUnique();
        builder.HasIndex(p => new { p.StartDate, p.EndDate });

        builder.Property(p => p.Name).HasMaxLength(64);
    }
}
