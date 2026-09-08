namespace EbedrendeloApp.Domain.Entities;

/// <summary>
/// A single reopen event for a <see cref="KitchenClosure"/> (US-6.2 AC 6.2.3). Kept as a separate,
/// append-only row rather than nullable columns on <see cref="KitchenClosure"/> itself, so the original
/// closing snapshot is never touched by a reopen (US-6.3 AC 6.3.2 — the snapshot must survive the
/// reopen unchanged, for audit).
/// </summary>
public sealed class KitchenClosureReopening
{
    public int Id { get; set; }
    public required int KitchenClosureId { get; set; }
    public KitchenClosure? KitchenClosure { get; set; }
    public DateTime ReopenedAtUtc { get; set; }
    public required int ReopenedByUserId { get; set; }
}
