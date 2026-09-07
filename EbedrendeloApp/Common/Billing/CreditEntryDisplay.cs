using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Billing.GetMyCreditLedger;
using MudBlazor;

namespace EbedrendeloApp.Common.Billing;

/// <summary>
/// Shared ledger-entry → label/icon/color/description mapping, mirroring
/// <see cref="EbedrendeloApp.Common.ALaCarte.ALaCarteCategoryDisplay"/>'s pattern — keeps this
/// presentation logic out of .razor files (CLAUDE.md).
/// </summary>
public static class CreditEntryDisplay
{
    public static string KindLabel(CreditEntryKind kind) => kind switch
    {
        CreditEntryKind.CancellationCredit => "Lemondási jóváírás",
        CreditEntryKind.CreditApplied => "Beszámítás",
        CreditEntryKind.CreditRevoked => "Visszavonás",
        CreditEntryKind.ManualAdjustment => "Kézi korrekció",
        _ => kind.ToString(),
    };

    public static Color KindColor(CreditEntryKind kind) => kind switch
    {
        CreditEntryKind.CancellationCredit => Color.Success,
        CreditEntryKind.CreditApplied => Color.Info,
        CreditEntryKind.CreditRevoked => Color.Error,
        CreditEntryKind.ManualAdjustment => Color.Warning,
        _ => Color.Default,
    };

    public static string KindIcon(CreditEntryKind kind) => kind switch
    {
        CreditEntryKind.CancellationCredit => Icons.Material.Filled.EventBusy,
        CreditEntryKind.CreditApplied => Icons.Material.Filled.ReceiptLong,
        CreditEntryKind.CreditRevoked => Icons.Material.Filled.Undo,
        CreditEntryKind.ManualAdjustment => Icons.Material.Filled.EditNote,
        _ => Icons.Material.Filled.AttachMoney,
    };

    /// <summary>A dátum előtti címke a történet-listában — csak <see cref="CreditEntryKind.CancellationCredit"/>
    /// jelent tényleges lemondást, a többi fajta (kézi korrekció, beszámítás, visszavonás) nem, ezért nem
    /// kaphat "Lemondva" feliratot.</summary>
    public static string DateLabel(CreditEntryKind kind) => kind == CreditEntryKind.CancellationCredit ? "Lemondva" : "Rögzítve";

    /// <summary>Egysoros "mi történt" szöveg (AC 5.3.1/5.3.2). Forrás-rendelés esetén (lemondás) csak a
    /// rendelés napja + variáns-kódja ("2026.08.10. A menü") — a teljes fogásnév és a rögzítő neve csak
    /// zajt adott hozzá egy már amúgy is dátum-oszloppal rendelkező listában. Rendelés nélkül a Note
    /// (indoklás), a rögzítő nevével; ha egyik sincs, a Kind neve, szintén a rögzítővel.</summary>
    public static string Describe(CreditLedgerEntryDto entry)
    {
        if (entry.SourceOrderDate is { } date)
        {
            var variant = string.IsNullOrWhiteSpace(entry.SourceOrderVariantCode) ? "" : $" {entry.SourceOrderVariantCode}";
            return $"{date:yyyy.MM.dd.}{variant} menü";
        }

        var label = string.IsNullOrWhiteSpace(entry.Note) ? KindLabel(entry.Kind) : entry.Note;
        return $"{label} ({entry.CreatedByDisplayName})";
    }
}
