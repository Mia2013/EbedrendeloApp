using EbedrendeloApp.Domain.Enums;

namespace EbedrendeloApp.Domain.Entities;

public sealed class CreditEntry
{
    public int Id { get; set; }
    public required int UserId { get; set; }
    public required int AmountHuf { get; set; }
    public required CreditEntryKind Kind { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public required int CreatedByUserId { get; set; }
    public string? Note { get; set; }
    public int? SourceMenuOrderId { get; set; }
    public int RemainingHuf { get; set; }
    public int? ConsumesCreditEntryId { get; set; }
    public int? PeriodInvoiceId { get; set; }
}
