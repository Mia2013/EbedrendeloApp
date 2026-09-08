using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Billing.GetInvoices;

/// <summary>US-7.3 — admin overview, optionally filtered by period and/or paid status (AC 7.3.1).</summary>
public sealed record GetInvoicesQuery(int? OrderingPeriodId, bool? IsPaid) : IRequest<Result<IReadOnlyList<InvoiceDto>>>;

/// <summary>AC 7.3.2 — full breakdown per row: gross menu/à la carte, applied credit, and the two payable
/// lines kept separate, plus the total.</summary>
public sealed record InvoiceDto(
    int Id,
    int UserId,
    string UserDisplayName,
    int OrderingPeriodId,
    string PeriodName,
    int MenuGrossHuf,
    int ALaCarteGrossHuf,
    int GrossHuf,
    int CreditAppliedHuf,
    int MenuPayableHuf,
    int ALaCartePayableHuf,
    int PayableHuf,
    bool IsPaid,
    DateTime? PaidAtUtc,
    DateTime GeneratedAtUtc);
