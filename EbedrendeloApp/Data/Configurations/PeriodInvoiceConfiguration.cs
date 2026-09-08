using EbedrendeloApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbedrendeloApp.Data.Configurations;

public sealed class PeriodInvoiceConfiguration : IEntityTypeConfiguration<PeriodInvoice>
{
    public void Configure(EntityTypeBuilder<PeriodInvoice> builder)
    {
        // Egy dolgozónak egy időszakra több számlája is lehet (alap + kiegészítő), ezért a párra nem
        // tehető unique index — a sorszámmal együtt viszont igen, és ez fogja meg azt is, ha két
        // párhuzamos generálás ugyanazt a következő sorszámot számolná ki.
        builder.HasIndex(i => new { i.UserId, i.OrderingPeriodId, i.SequenceNumber }).IsUnique();

        builder.HasOne<User>().WithMany().HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(i => i.MarkedPaidByUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrderingPeriod>().WithMany().HasForeignKey(i => i.OrderingPeriodId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
