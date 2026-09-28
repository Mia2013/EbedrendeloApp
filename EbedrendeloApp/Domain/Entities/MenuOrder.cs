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

    /// <summary>
    /// Melyik számla fedezi ezt a napot; <c>null</c> = még nincs kiszámlázva. Ez a mező a számlázás
    /// igazságforrása a „van-e egyáltalán <see cref="PeriodInvoice"/> erre az időszakra" kérdés helyett:
    /// <list type="bullet">
    /// <item>a számlagenerálás csak a még <c>null</c> sorokat számlázza (delta-számlázás), így a számla
    /// kiállítása után leadott rendelés kiegészítő számlát kap, nem marad ingyen;</item>
    /// <item>lemondáskor csak akkor keletkezik jóváírás, ha ez a mező ki van töltve — egy soha ki nem
    /// számlázott nap lemondásáért nem jár pénz vissza.</item>
    /// </list>
    /// </summary>
    public int? PeriodInvoiceId { get; set; }
}
