using EbedrendeloApp.Domain.Enums;
using MudBlazor;

namespace EbedrendeloApp.Common.Notifications;

/// <summary>
/// Értesítés-típus → ikon/szín, a <see cref="Billing.CreditEntryDisplay"/> mintájára — a megjelenítési
/// döntés egy helyen, nem a .razor-ban (CLAUDE.md). A szín a repó szemantikus konvenciója
/// (<c>ebedrendelo-extensions</c> skill): elvesztett rendelés hiba, átvezetés figyelmeztetés, visszakapott
/// rendelés és jóváírás siker, puszta tájékoztatás info.
/// </summary>
public static class NotificationDisplay
{
    public static string Icon(NotificationType type) => type switch
    {
        NotificationType.MenuChanged => Icons.Material.Filled.EditCalendar,
        NotificationType.OrderReassigned => Icons.Material.Filled.SwapHoriz,
        NotificationType.MenuCancelled => Icons.Material.Filled.EventBusy,
        NotificationType.OrderRestored => Icons.Material.Filled.Restore,
        NotificationType.DayReopened => Icons.Material.Filled.EventAvailable,
        NotificationType.CreditIssued => Icons.Material.Filled.Savings,
        NotificationType.CreditApplied => Icons.Material.Filled.ReceiptLong,
        _ => Icons.Material.Filled.Notifications,
    };

    public static Color Color(NotificationType type) => type switch
    {
        NotificationType.MenuChanged => MudBlazor.Color.Info,
        NotificationType.OrderReassigned => MudBlazor.Color.Warning,
        NotificationType.MenuCancelled => MudBlazor.Color.Error,
        NotificationType.OrderRestored => MudBlazor.Color.Success,
        NotificationType.DayReopened => MudBlazor.Color.Info,
        NotificationType.CreditIssued => MudBlazor.Color.Success,
        NotificationType.CreditApplied => MudBlazor.Color.Info,
        _ => MudBlazor.Color.Default,
    };
}
