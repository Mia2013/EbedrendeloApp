using EbedrendeloApp.Domain.Enums;

namespace EbedrendeloApp.Common.Orders;

/// <summary>
/// Shared <see cref="CancellationReason"/> → Hungarian text mapping, used by AdminOrders.razor and
/// MyOrders.razor — kept together so a new <see cref="CancellationReason"/> value is easy to notice
/// as missing from both. The two wordings are deliberately different (terse lowercase phrase for the
/// admin log vs. a full sentence for the worker-facing list), same as <c>DayUnavailableReasonText</c>
/// keeps TodayMenu.razor's wording separate — this is not a case of accidental duplication to unify.
/// </summary>
public static class CancellationReasonDisplay
{
    private static readonly Dictionary<CancellationReason, string> AdminReasonText = new()
    {
        [CancellationReason.ByUser] = "saját lemondás",
        [CancellationReason.DayExcluded] = "nap kizárása",
        [CancellationReason.MenuDeleted] = "menü törölve",
        [CancellationReason.VariantRemoved] = "menüvariáns megszűnt",
    };

    private static readonly Dictionary<CancellationReason, string> UserReasonText = new()
    {
        [CancellationReason.ByUser] = "Saját lemondás",
        [CancellationReason.DayExcluded] = "Nap kizárása miatt",
        [CancellationReason.MenuDeleted] = "A menü törölve lett",
        [CancellationReason.VariantRemoved] = "A menüvariáns megszűnt",
    };

    public static string AdminText(CancellationReason? reason) =>
        reason is { } r ? AdminReasonText.GetValueOrDefault(r, r.ToString()) : string.Empty;

    public static string UserText(CancellationReason? reason) =>
        reason is { } r ? UserReasonText.GetValueOrDefault(r, r.ToString()) : string.Empty;
}
