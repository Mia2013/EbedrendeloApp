namespace EbedrendeloApp.Common.Results;

/// <summary>
/// Shared Hungarian text for "why is this day locked" reason codes — used by the calendar cells
/// (UserCalendar.razor) and the batch-operation result dialog (PlaceOrderResultDialog.razor), which
/// must show identical wording for the same <see cref="ErrorCodes"/>. Deliberately NOT reused by
/// TodayMenu.razor — that page's wording is context-specific ("ma..." phrasing) and intentionally
/// differs from these generic reasons.
/// </summary>
public static class DayUnavailableReasonText
{
    private static readonly Dictionary<string, string> Text = new()
    {
        [ErrorCodes.DeadlinePassed] = "A módosítási határidő lejárt",
        [ErrorCodes.DayClosed] = "A nap már le van zárva",
        [ErrorCodes.DayExcluded] = "Kizárt nap",
        [ErrorCodes.NotWorkingDay] = "Nem munkanap",
        [ErrorCodes.MenuNotPublished] = "Erre a napra még nincs publikált menü",
        [ErrorCodes.OutsidePeriod] = "A nap az időszakon kívül esik",
        [ErrorCodes.AlreadyOrdered] = "Már van aktív rendelésed erre a napra",
        [ErrorCodes.NoActiveOrder] = "Nincs aktív rendelésed erre a napra",
        [ErrorCodes.PeriodClosed] = "Az időszak le van zárva",
        [ErrorCodes.InvalidVariantCode] = "A kiválasztott menü nem érvényes",
    };

    public static string Describe(string? reason) => reason is null ? string.Empty : Text.GetValueOrDefault(reason, reason);
}
