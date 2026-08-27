using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Calendar.ExcludeDay;

public sealed record ExcludeDayCommand(DateOnly Date, string Reason, int CreatedByUserId) : IRequest<Result>
{
    /// <summary>
    /// Single source of truth for the Reason field's max length — referenced by ExcludeDayValidator's
    /// server-side rule and ExcludeDayDialog.razor's UI guard/MaxLength/Counter, so the business rule can't
    /// drift between the two.
    /// </summary>
    public const int ReasonMaxLength = 200;
}
