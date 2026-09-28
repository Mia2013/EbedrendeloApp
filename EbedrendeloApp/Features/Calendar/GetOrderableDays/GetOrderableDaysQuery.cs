using EbedrendeloApp.Common.Security;
using MediatR;

namespace EbedrendeloApp.Features.Calendar.GetOrderableDays;

/// <summary>A kolléga nevében rendeléshez az ő naptárát is látni kell, ezért ez a lekérdezés is a
/// „bárki bárkinek, auditálva" körbe tartozik (AC 3.1.6) — a címzettet előtte azonosítani kell
/// (<c>ResolveColleagueQuery</c>). A pénzügyi lekérdezések (egyenleg, jóváírás-történet, számlák)
/// ezzel szemben idegen felhasználóra továbbra is admin-jogot kívánnak.</summary>
public sealed record GetOrderableDaysQuery(int OrderingPeriodId, int UserId)
    : IRequest<Common.Results.Result<IReadOnlyList<OrderableDayDto>>>, IAuditedOnBehalfOf;

public sealed record OrderableDayDto(
    DateOnly Date,
    bool Orderable,
    bool Cancellable,
    string? VariantCode,
    string? VariantName,
    string? Reason,
    string? ReasonDetail,
    int MenuPortionHuf = 0);
