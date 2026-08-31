namespace EbedrendeloApp.Features.Orders;

/// <summary>
/// Shared result shape for both batch commands (01-szerver-architektura.md 3.5 "Köteges rendelés és
/// lemondás — részleges siker"). <see cref="Skipped"/> non-empty means the caller must not report the
/// operation as a plain success — the UI is required to surface the skipped days and their reasons
/// (AC 3.1.4 / AC 3.2.5).
/// </summary>
public sealed record BatchOrderResult(IReadOnlyList<DayResult> Succeeded, IReadOnlyList<DaySkip> Skipped);

public sealed record DayResult(DateOnly Date, string VariantCode);

/// <summary>Reason is one of <see cref="Common.Results.ErrorCodes"/>, never free text.</summary>
public sealed record DaySkip(DateOnly Date, string Reason);

/// <summary>
/// A daily menu often reuses the same soup across several variants (A/B/C) — a "VariantName" that only
/// echoed the soup would then collapse to the same string for two different orders, defeating the point
/// of showing a name at all (AC 3.3.1 / AC 3.4.2, used for admin reconciliation). Combining the soup with
/// the main course (when the variant has one) keeps every variant's name distinct.
/// </summary>
public static class VariantDisplayName
{
    public static string Combine(string soupName, string? mainCourseName) =>
        string.IsNullOrEmpty(mainCourseName) ? soupName : $"{soupName} + {mainCourseName}";
}
