using EbedrendeloApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbedrendeloApp.Data.Configurations;

public sealed class ExcludedDayConfiguration : IEntityTypeConfiguration<ExcludedDay>
{
    public void Configure(EntityTypeBuilder<ExcludedDay> builder)
    {
        builder.HasIndex(e => e.Date).IsUnique();
        builder.Property(e => e.Reason).HasMaxLength(200);

        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(e => e.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
