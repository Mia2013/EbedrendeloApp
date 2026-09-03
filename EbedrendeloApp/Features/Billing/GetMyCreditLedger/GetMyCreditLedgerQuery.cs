using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Enums;
using MediatR;

namespace EbedrendeloApp.Features.Billing.GetMyCreditLedger;

public sealed record GetMyCreditLedgerQuery(int UserId) : IRequest<Result<IReadOnlyList<CreditLedgerEntryDto>>>;

/// <summary>AC 5.3.1-5.3.3 — one row per append-only <c>CreditEntry</c>. <see cref="SourceOrderDate"/>/
/// <see cref="SourceOrderVariantName"/> are resolved from <see cref="SourceMenuOrderId"/> ("mi lett
/// lemondva"). <see cref="PeriodInvoiceId"/> is exposed as a raw id (no join) for forward compatibility
/// with Epic 7's billing — it is always null until <c>GeneratePeriodInvoicesCommand</c> exists.</summary>
public sealed record CreditLedgerEntryDto(
    int Id,
    CreditEntryKind Kind,
    int AmountHuf,
    int RemainingHuf,
    DateTime CreatedAtUtc,
    string? Note,
    int CreatedByUserId,
    string CreatedByDisplayName,
    int? SourceMenuOrderId,
    DateOnly? SourceOrderDate,
    string? SourceOrderVariantName,
    int? ConsumesCreditEntryId,
    int? PeriodInvoiceId);
