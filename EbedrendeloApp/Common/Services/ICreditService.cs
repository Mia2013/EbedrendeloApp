using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;

namespace EbedrendeloApp.Common.Services;

/// <summary>
/// Ledger operations for issuing and revoking credit (01-szerver-architektura.md 3.3), and manual
/// adjustments per US-5.2. Operates on the caller's <see cref="EbedrendeloDbContext"/> and does not call
/// SaveChanges — the handler owns the unit of work and commits everything (order status, credit entry,
/// notification) together.
/// </summary>
public interface ICreditService
{
    CreditEntry IssueCancellationCredit(EbedrendeloDbContext db, MenuOrder order, int createdByUserId, DateTime nowUtc);

    void RevokeCredit(EbedrendeloDbContext db, CreditEntry original, int createdByUserId, DateTime nowUtc, string note);

    /// <summary>AC 5.2.2 — a positive, admin-issued adjustment (e.g. compensation for a same-day kitchen
    /// incident, AC 5.2.1/KL-3), immediately usable like a cancellation credit. Amount must be positive;
    /// a manual debit is out of scope (would need its own method, since <c>RemainingHuf</c> only has
    /// clean semantics on positive entries).</summary>
    CreditEntry IssueManualCredit(EbedrendeloDbContext db, int userId, int amountHuf, int createdByUserId, DateTime nowUtc, string note);
}
