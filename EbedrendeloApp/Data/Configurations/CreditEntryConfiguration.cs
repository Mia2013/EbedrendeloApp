using EbedrendeloApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbedrendeloApp.Data.Configurations;

public sealed class CreditEntryConfiguration : IEntityTypeConfiguration<CreditEntry>
{
    public void Configure(EntityTypeBuilder<CreditEntry> builder)
    {
        builder.Property(c => c.Note).HasMaxLength(500);

        builder.HasOne<User>().WithMany().HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(c => c.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MenuOrder>().WithMany().HasForeignKey(c => c.SourceMenuOrderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CreditEntry>().WithMany().HasForeignKey(c => c.ConsumesCreditEntryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PeriodInvoice>().WithMany().HasForeignKey(c => c.PeriodInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
