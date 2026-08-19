namespace EbedrendeloApp.Domain.Entities;

public sealed class KitchenClosure
{
    public int Id { get; set; }
    public required DateOnly Date { get; set; }
    public DateTime ClosedAtUtc { get; set; }
    public required int ClosedByUserId { get; set; }
    public required int TotalPortions { get; set; }
    public List<KitchenClosureLine> Lines { get; set; } = [];
}
