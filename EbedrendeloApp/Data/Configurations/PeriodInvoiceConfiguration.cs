using EbedrendeloApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbedrendeloApp.Data.Configurations;

public sealed class PeriodInvoiceConfiguration : IEntityTypeConfiguration<PeriodInvoice>
{
    public void Configure(EntityTypeBuilder<PeriodInvoice> builder)
    {
        builder.HasIndex(i => new { i.UserId, i.OrderingPeriodId }).IsUnique();

        builder.HasOne<User>().WithMany().HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(i => i.MarkedPaidByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrderingPeriod>().WithMany().HasForeignKey(i => i.OrderingPeriodId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
