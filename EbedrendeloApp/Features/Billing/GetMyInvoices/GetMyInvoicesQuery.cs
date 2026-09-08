using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Billing.GetMyInvoices;

/// <summary>US-7.4 — the requesting user's own period invoices (AC 7.4.1/7.4.2).</summary>
public sealed record GetMyInvoicesQuery(int UserId) : IRequest<Result<IReadOnlyList<MyInvoiceDto>>>;

/// <param name="SequenceNumber">1 = alapszámla, 2+ = kiegészítő számla ugyanarra az időszakra.</param>
/// <param name="DayCount">Hány menünap tartozik a számlához.</param>
public sealed record MyInvoiceDto(
    int Id,
    int OrderingPeriodId,
    string PeriodName,
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    int SequenceNumber,
    int DayCount,
    int GrossHuf,
    int CreditAppliedHuf,
    int PayableHuf,
    bool IsPaid,
    DateTime? PaidAtUtc,
    DateTime GeneratedAtUtc);
