using MediatR;

namespace EbedrendeloApp.Features.Calendar.GetExcludedDays;

public sealed record GetExcludedDaysQuery(DateOnly From, DateOnly To) : IRequest<IReadOnlyList<ExcludedDayDto>>;

public sealed record ExcludedDayDto(DateOnly Date, string Reason, string CreatedByDisplayName, DateTime CreatedAtUtc);
