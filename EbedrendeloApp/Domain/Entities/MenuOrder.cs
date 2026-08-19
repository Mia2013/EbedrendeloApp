using EbedrendeloApp.Domain.Enums;

namespace EbedrendeloApp.Domain.Entities;

public sealed class MenuOrder
{
    public int Id { get; set; }
    public required int UserId { get; set; }
    public required DateOnly Date { get; set; }
    public required int OrderingPeriodId { get; set; }
    public required int MenuVariantId { get; set; }
    public required int PriceHuf { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Active;
    public required int PlacedByUserId { get; set; }
    public DateTime PlacedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public int? CancelledByUserId { get; set; }
    public CancellationReason? CancellationReason { get; set; }
    public int? CancelledByExcludedDayId { get; set; }
    public string? ReassignedFromVariantCode { get; set; }
    public DateTime? ReassignedAtUtc { get; set; }
}
