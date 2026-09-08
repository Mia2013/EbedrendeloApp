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
    /// <summary>
    /// Jóváírást ír a lemondott rendelésről — **de csak akkor, ha a rendelés már ki volt számlázva**
    /// (<see cref="MenuOrder.PeriodInvoiceId"/> nem <c>null</c>), egyébként <c>null</c>-t ad vissza és
    /// nem ír semmit. Ki nem számlázott napért nem jár pénz vissza: azt a dolgozó soha nem fizette ki,
    /// a delta-számlázás pedig eleve nem fogja rátenni egyetlen számlára sem. A feltétel szándékosan itt
    /// van és nem a hívókban — öt helyről hívjuk (lemondás, nap kizárása, napi menü törlése,
    /// variáns-átvezetés), és egy kifelejtett ellenőrzés ingyen pénzt osztana.
    /// </summary>
    CreditEntry? IssueCancellationCredit(EbedrendeloDbContext db, MenuOrder order, int createdByUserId, DateTime nowUtc);

    void RevokeCredit(EbedrendeloDbContext db, CreditEntry original, int createdByUserId, DateTime nowUtc, string note);

    /// <summary>AC 5.2.2 — a positive, admin-issued adjustment (e.g. compensation for a same-day kitchen
    /// incident, AC 5.2.1/KL-3), immediately usable like a cancellation credit. Amount must be positive;
    /// a manual debit is out of scope (would need its own method, since <c>RemainingHuf</c> only has
    /// clean semantics on positive entries).</summary>
    CreditEntry IssueManualCredit(EbedrendeloDbContext db, int userId, int amountHuf, int createdByUserId, DateTime nowUtc, string note);

    /// <summary>AC 7.1.2/7.1.3 — FIFO drawdown of a user's available credit against the invoice's
    /// <paramref name="grossHuf"/>. <paramref name="availableCreditsFifoOrdered"/>
    /// must already be filtered to <c>RemainingHuf &gt; 0</c> and ordered by <c>CreatedAtUtc</c> ascending —
    /// this method mutates each consumed entry's <c>RemainingHuf</c> in place (tracked by the caller's
    /// <paramref name="db"/>, so <c>SaveChangesAsync</c> persists it) and adds a new <c>CreditApplied</c>
    /// entry per consumed source. The returned entries' <c>PeriodInvoiceId</c> is left unset — the invoice
    /// row doesn't have a real <c>Id</c> yet at this point (two-phase save, mirroring
    /// <c>CloseDayHandler</c>'s <c>KitchenClosureLine</c>s) — the caller stamps it in once known.</summary>
    CreditApplicationResult ApplyCreditToInvoice(
        EbedrendeloDbContext db, IReadOnlyList<CreditEntry> availableCreditsFifoOrdered, int grossHuf, int createdByUserId, DateTime nowUtc);
}

public sealed record CreditApplicationResult(int TotalAppliedHuf, IReadOnlyList<CreditEntry> AppliedEntries);
