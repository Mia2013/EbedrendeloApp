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

    /// <summary>Egysoros "mi történt" szöveg (AC 5.3.1/5.3.2): forrás-rendelés (dátum+variáns), ha van,
    /// plusz mindig a Note (indoklás/visszavonás oka), ha van; ha egyik sincs, a Kind neve.</summary>
    public static string Describe(CreditLedgerEntryDto entry)
    {
        var parts = new List<string>();

        if (entry.SourceOrderDate is { } date)
        {
            var variant = string.IsNullOrWhiteSpace(entry.SourceOrderVariantName) ? "" : $" ({entry.SourceOrderVariantName})";
            parts.Add($"{date:yyyy.MM.dd.}{variant} rendelés");
        }

        if (!string.IsNullOrWhiteSpace(entry.Note))
        {
            parts.Add(entry.Note);
        }

        if (parts.Count == 0)
        {
            parts.Add(KindLabel(entry.Kind));
        }

        return string.Join(" — ", parts) + $" ({entry.CreatedByDisplayName})";
    }
}
