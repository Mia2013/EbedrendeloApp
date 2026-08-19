namespace EbedrendeloApp.Domain.Entities;

public sealed class PeriodInvoice
{
    public int Id { get; set; }
    public required int UserId { get; set; }
    public required int OrderingPeriodId { get; set; }
    public required int MenuGrossHuf { get; set; }
    public required int ALaCarteGrossHuf { get; set; }
    public required int GrossHuf { get; set; }
    public required int CreditAppliedHuf { get; set; }
    public required int MenuPayableHuf { get; set; }
    public required int ALaCartePayableHuf { get; set; }
    public required int PayableHuf { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public int? MarkedPaidByUserId { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
}
