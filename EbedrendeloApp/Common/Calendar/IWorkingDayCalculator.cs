using EbedrendeloApp.Domain.Entities;

namespace EbedrendeloApp.Common.Calendar;

/// <summary>
/// Pure calendar math — no data access. Callers load <see cref="AppSetting"/> and the set of
/// excluded dates once (typically per handler invocation) and pass them in, so this stays
/// deterministic and testable without a database (see 01-szerver-architektura.md 3.1, 9. fejezet).
/// </summary>
public interface IWorkingDayCalculator
{
    bool IsWorkingDay(DateOnly date, IReadOnlySet<DateOnly> excludedDates);

    /// <summary>
    /// The local datetime by which a change (order or cancellation) for <paramref name="serviceDate"/>
    /// must happen — counts back <see cref="AppSetting.ChangeDeadlineWorkingDays"/> working days,
    /// skipping weekends and <paramref name="excludedDates"/>.
    /// </summary>
    DateTime ChangeDeadline(DateOnly serviceDate, AppSetting settings, IReadOnlySet<DateOnly> excludedDates);

    bool CanChange(DateOnly serviceDate, DateTime nowLocal, AppSetting settings, IReadOnlySet<DateOnly> excludedDates, bool hasKitchenClosure);
}
