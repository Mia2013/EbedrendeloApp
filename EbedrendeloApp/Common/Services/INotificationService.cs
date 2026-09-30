using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;

namespace EbedrendeloApp.Common.Services;

/// <summary>
/// Adds in-app notifications (01-szerver-architektura.md Epic 8 / US-8.1). Operates on the caller's
/// <see cref="EbedrendeloDbContext"/> and does not call SaveChanges — see <see cref="ICreditService"/>.
/// </summary>
public interface INotificationService
{
    /// <summary>Egyetlen címzettnek szóló értesítés — rendeléshez nem kötött eseményre (kézi jóváírás,
    /// számla). Rendelés-eseményre a <see cref="NotifyOrderParties"/> való.</summary>
    void Notify(
        EbedrendeloDbContext db,
        int userId,
        NotificationType type,
        string title,
        string message,
        DateTime nowUtc,
        DateOnly? relatedDate = null,
        int? relatedMenuOrderId = null);

    /// <summary>
    /// Egy rendelést érintő esemény értesítése a rendelés <b>tulajdonosának</b>, és ha a rendelést más adta
    /// le, a <b>leadónak</b> is (AC 8.1.3, AC 2.2.2) — a leadó saját, „az általad leadott…" szövegezést kap.
    /// A leadó kimarad, ha ő maga végezte a műveletet (<paramref name="performedByUserId"/>): amit maga
    /// csinált, arról nem kell értesíteni. Lemondásra a <see cref="NotifyOrderCancelled"/> való.
    /// </summary>
    void NotifyOrderParties(
        EbedrendeloDbContext db,
        MenuOrder order,
        NotificationType type,
        OrderNotificationText owner,
        OrderNotificationText placer,
        int performedByUserId,
        DateTime nowUtc);

    /// <summary>
    /// Lemondás értesítése — minden lemondási útvonal (saját lemondás, nap kizárása, menü- és
    /// variánstörlés) ezt hívja, hogy a típus egy helyen dőljön el (01 §3.3): ha a lemondás
    /// <paramref name="credit"/>-et szült, a tulajdonos <c>CreditIssued</c>-ot kap, egyébként
    /// <c>MenuCancelled</c>-et. A leadó mindig <c>MenuCancelled</c>-et kap — a jóváírás csak a tulajdonost
    /// illeti, a leadónak ez puszta lemondás. A címzettség a <see cref="NotifyOrderParties"/> szabálya.
    /// </summary>
    void NotifyOrderCancelled(
        EbedrendeloDbContext db,
        MenuOrder order,
        CreditEntry? credit,
        OrderNotificationText owner,
        OrderNotificationText placer,
        int performedByUserId,
        DateTime nowUtc);
}

/// <summary>Egy értesítés címe és szövege — a tulajdonosnak és a leadónak külön példány készül.</summary>
public sealed record OrderNotificationText(string Title, string Message);
