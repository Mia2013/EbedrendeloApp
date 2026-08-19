using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;

namespace EbedrendeloApp.Common.Services;

/// <summary>
/// Ledger operations for cancellation credits (01-szerver-architektura.md 3.3). Operates on the
/// caller's <see cref="EbedrendeloDbContext"/> and does not call SaveChanges — the handler owns the
/// unit of work and commits everything (order status, credit entry, notification) together.
/// </summary>
public interface ICreditService
{
    CreditEntry IssueCancellationCredit(EbedrendeloDbContext db, MenuOrder order, int createdByUserId, DateTime nowUtc);

    void RevokeCredit(EbedrendeloDbContext db, CreditEntry original, int createdByUserId, DateTime nowUtc, string note);
}
