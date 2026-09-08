namespace EbedrendeloApp.Domain.Entities;

public sealed class KitchenClosure
{
    public int Id { get; set; }
    public required DateOnly Date { get; set; }
    public DateTime ClosedAtUtc { get; set; }
    public required int ClosedByUserId { get; set; }
    public required int TotalPortions { get; set; }
    public List<KitchenClosureLine> Lines { get; set; } = [];

    /// <summary>Set once this closure has been reopened (at most one — see <see cref="KitchenClosureReopening"/>).
    /// A day can be closed again after this, which inserts a brand-new <see cref="KitchenClosure"/> row
    /// rather than reusing this one — see <c>Common/Services/KitchenClosureQueries.cs</c> for what
    /// "currently closed" means.</summary>
    public KitchenClosureReopening? Reopening { get; set; }
}
