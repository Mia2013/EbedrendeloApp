namespace EbedrendeloApp.Domain.Entities;

public sealed class ALaCarteOrder
{
    public int Id { get; set; }
    public required int UserId { get; set; }
    public required DateOnly Date { get; set; }
    public required int OrderingPeriodId { get; set; }
    public DateTime PlacedAtUtc { get; set; }
    public required int PlacedByUserId { get; set; }
    public required int TotalHuf { get; set; }
    public List<ALaCarteOrderLine> Lines { get; set; } = [];
}
