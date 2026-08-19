using EbedrendeloApp.Domain.Entities;

namespace EbedrendeloApp.Common.Calendar;

public sealed class WorkingDayCalculator : IWorkingDayCalculator
{
    public bool IsWorkingDay(DateOnly date, IReadOnlySet<DateOnly> excludedDates)
        => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !excludedDates.Contains(date);

    public DateTime ChangeDeadline(DateOnly serviceDate, AppSetting settings, IReadOnlySet<DateOnly> excludedDates)
    {
        var date = serviceDate;
        var counted = 0;
        while (counted < settings.ChangeDeadlineWorkingDays)
        {
            date = date.AddDays(-1);
            if (IsWorkingDay(date, excludedDates))
            {
                counted++;
            }
        }

        return date.ToDateTime(settings.ChangeDeadlineLocalTime);
    }

    public bool CanChange(DateOnly serviceDate, DateTime nowLocal, AppSetting settings, IReadOnlySet<DateOnly> excludedDates, bool hasKitchenClosure)
        => !hasKitchenClosure && nowLocal <= ChangeDeadline(serviceDate, settings, excludedDates);
}
