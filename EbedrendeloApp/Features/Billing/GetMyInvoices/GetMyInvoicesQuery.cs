using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Billing.GetMyInvoices;

/// <summary>US-7.4 — the requesting user's own period invoices (AC 7.4.1/7.4.2).</summary>
public sealed record GetMyInvoicesQuery(int UserId) : IRequest<Result<IReadOnlyList<MyInvoiceDto>>>;

public sealed record MyInvoiceDto(
    int Id,
    int OrderingPeriodId,
    string PeriodName,
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
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
