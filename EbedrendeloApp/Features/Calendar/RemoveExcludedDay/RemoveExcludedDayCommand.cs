using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Calendar.RemoveExcludedDay;

public sealed record RemoveExcludedDayCommand(DateOnly Date, bool RestoreCancelledOrders, int PerformedByUserId)
    : IRequest<Result<RemoveExcludedDayResult>>;

public sealed record RemoveExcludedDayResult(int RestoredCount, int SkippedCount, IReadOnlyList<SkippedOrderInfo> SkippedDetails);

public sealed record SkippedOrderInfo(string UserDisplayName, string Reason);
