using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Billing.GeneratePeriodInvoices;

/// <summary>US-7.1 — generates one <c>PeriodInvoice</c> per user who has period activity (an Active
/// <c>MenuOrder</c> or an <c>ALaCarteOrder</c>) and does not already have one for this period (AC 7.1.6).
/// Only allowed once the period's bulk ordering window has closed (<c>OrderDeadline</c> passed) —
/// <c>IsOpen</c> is not the right gate here, because it also controls in-month day-by-day cancellation
/// (01-szerver-architektura.md 3.1) and must stay usable through the eating period.</summary>
public sealed record GeneratePeriodInvoicesCommand(int OrderingPeriodId, int GeneratedByUserId)
    : IRequest<Result<BatchInvoiceResult>>;

public sealed record BatchInvoiceResult(
    IReadOnlyList<GeneratedInvoiceDto> Generated, IReadOnlyList<int> SkippedAlreadyInvoicedUserIds);

public sealed record GeneratedInvoiceDto(
    int InvoiceId,
    int UserId,
    int MenuGrossHuf,
    int ALaCarteGrossHuf,
    int CreditAppliedHuf,
    int MenuPayableHuf,
    int ALaCartePayableHuf,
    int PayableHuf);
