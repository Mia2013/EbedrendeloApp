using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;

namespace EbedrendeloApp.Common.Services;

public sealed class CreditService : ICreditService
{
    public CreditEntry IssueCancellationCredit(EbedrendeloDbContext db, MenuOrder order, int createdByUserId, DateTime nowUtc)
    {
        var entry = new CreditEntry
        {
            UserId = order.UserId,
            AmountHuf = order.PriceHuf,
            Kind = CreditEntryKind.CancellationCredit,
            CreatedAtUtc = nowUtc,
            CreatedByUserId = createdByUserId,
            SourceMenuOrderId = order.Id,
            RemainingHuf = order.PriceHuf,
        };

        db.CreditEntries.Add(entry);
        return entry;
    }

    public void RevokeCredit(EbedrendeloDbContext db, CreditEntry original, int createdByUserId, DateTime nowUtc, string note)
    {
        db.CreditEntries.Add(new CreditEntry
        {
            UserId = original.UserId,
            AmountHuf = -original.AmountHuf,
            Kind = CreditEntryKind.CreditRevoked,
            CreatedAtUtc = nowUtc,
            CreatedByUserId = createdByUserId,
            ConsumesCreditEntryId = original.Id,
            Note = note,
            RemainingHuf = 0,
        });

        original.RemainingHuf = 0;
    }

    public CreditEntry IssueManualCredit(EbedrendeloDbContext db, int userId, int amountHuf, int createdByUserId, DateTime nowUtc, string note)
    {
        var entry = new CreditEntry
        {
            UserId = userId,
            AmountHuf = amountHuf,
            Kind = CreditEntryKind.ManualAdjustment,
            CreatedAtUtc = nowUtc,
            CreatedByUserId = createdByUserId,
            Note = note,
            RemainingHuf = amountHuf,
        };

        db.CreditEntries.Add(entry);
        return entry;
    }
}
