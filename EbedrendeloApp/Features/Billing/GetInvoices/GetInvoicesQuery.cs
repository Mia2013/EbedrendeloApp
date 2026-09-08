using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Billing.GetInvoices;

/// <summary>US-7.3 — admin overview, optionally filtered by period and/or paid status (AC 7.3.1).</summary>
public sealed record GetInvoicesQuery(int? OrderingPeriodId, bool? IsPaid) : IRequest<Result<IReadOnlyList<InvoiceDto>>>, IRequireAdmin;

/// <summary>
/// AC 7.3.2 — soronkénti bontás: bruttó menü, beszámított jóváírás, fizetendő. À la carte nincs rajta,
/// azt a dolgozó aznap fizeti.
/// </summary>
/// <param name="SequenceNumber">1 = alapszámla, 2+ = kiegészítő számla ugyanarra az időszakra.</param>
/// <param name="DayCount">Hány menünap tartozik a számlához.</param>
public sealed record InvoiceDto(
    int Id,
    int UserId,
    string UserDisplayName,
    int OrderingPeriodId,
    string PeriodName,
    int SequenceNumber,
    int DayCount,
    int GrossHuf,
    int CreditAppliedHuf,
    int PayableHuf,
    bool IsPaid,
    DateTime? PaidAtUtc,
    DateTime GeneratedAtUtc);
