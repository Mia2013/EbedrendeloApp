using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Calendar.ExcludeDay;

public sealed record ExcludeDayCommand(DateOnly Date, string Reason, int CreatedByUserId) : IRequest<Result>;
