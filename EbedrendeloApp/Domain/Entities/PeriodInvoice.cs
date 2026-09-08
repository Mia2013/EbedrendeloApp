namespace EbedrendeloApp.Domain.Entities;

/// <summary>
/// Egy időszaki menü-számla. **Nem** a (felhasználó, időszak) párra szól, hanem egy konkrét
/// rendelés-halmazra: a hozzá tartozó napokat a <see cref="MenuOrder.PeriodInvoiceId"/> jelöli. Ezért
/// egy dolgozónak egy időszakra több számlája is lehet — az elsőt a tömeges leadási határidő után
/// állítjuk ki, a későbbi (B-fázisú, pótlólagos) rendelésekről pedig kiegészítő számla készül.
///
/// À la carte tétel nincs rajta: azt a dolgozó aznap fizeti, nem az időszaki elszámolásban.
/// </summary>
public sealed class PeriodInvoice
{
    public int Id { get; set; }
    public required int UserId { get; set; }
    public required int OrderingPeriodId { get; set; }

    /// <summary>1-től induló sorszám felhasználónként és időszakonként. 1 = alapszámla, 2+ = a korábbi
    /// számla kiállítása után rendelt napok kiegészítő számlája.</summary>
    public required int SequenceNumber { get; set; }

    /// <summary>A számlához tartozó menürendelések bruttó összege (snapshot, NFR-7).</summary>
    public required int GrossHuf { get; set; }

    /// <summary>A beszámított jóváírás — legfeljebb <see cref="GrossHuf"/>.</summary>
    public required int CreditAppliedHuf { get; set; }

    /// <summary><see cref="GrossHuf"/> − <see cref="CreditAppliedHuf"/>.</summary>
    public required int PayableHuf { get; set; }

    public bool IsPaid { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public int? MarkedPaidByUserId { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
}
