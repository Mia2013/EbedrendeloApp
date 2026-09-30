using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Tests.TestSupport;

namespace EbedrendeloApp.Tests.Common.Services;

/// <summary>AC 8.1.3 — a rendelés-esemény címzettjei. A szabály egy helyen él
/// (<see cref="NotificationService.NotifyOrderParties"/>), ezért itt minden ágát lefedjük; a handlerek
/// bekötését a saját tesztjeik egy-egy „más nevében leadott" esettel ellenőrzik.</summary>
public class NotificationServiceTests : IDisposable
{
    private const int Owner = 10;
    private const int Colleague = 20;
    private const int Admin = 30;

    private static readonly DateTime NowUtc = new(2026, 8, 17, 7, 0, 0, DateTimeKind.Utc);

    private static readonly OrderNotificationText OwnerText = new("Rendelésed lemondásra került", "tulajdonosi szöveg");
    private static readonly OrderNotificationText PlacerText = new("Az általad leadott rendelés lemondásra került", "leadói szöveg");

    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly NotificationService sut = new();

    public void Dispose() => dbFactory.Dispose();

    private static MenuOrder Order(int placedBy) => new()
    {
        Id = 5,
        UserId = Owner,
        Date = new DateOnly(2026, 8, 20),
        OrderingPeriodId = 1,
        MenuVariantId = 1,
        PriceHuf = 1400,
        PlacedByUserId = placedBy,
    };

    private IReadOnlyList<UserNotification> Notify(MenuOrder order, int performedBy)
    {
        using var db = dbFactory.CreateDbContext();
        sut.NotifyOrderParties(db, order, NotificationType.MenuCancelled, OwnerText, PlacerText, performedBy, NowUtc);
        return db.UserNotifications.Local.ToList();
    }

    [Fact]
    public void An_own_order_notifies_only_the_owner()
    {
        var notifications = Notify(Order(placedBy: Owner), performedBy: Admin);

        var single = Assert.Single(notifications);
        Assert.Equal(Owner, single.UserId);
        Assert.Equal("tulajdonosi szöveg", single.Message);
    }

    [Fact]
    public void An_order_placed_by_a_colleague_notifies_both_with_their_own_wording()
    {
        var notifications = Notify(Order(placedBy: Colleague), performedBy: Admin);

        Assert.Equal(2, notifications.Count);
        var owner = Assert.Single(notifications, n => n.UserId == Owner);
        var placer = Assert.Single(notifications, n => n.UserId == Colleague);
        Assert.Equal(OwnerText.Title, owner.Title);
        Assert.Equal(PlacerText.Title, placer.Title);
        Assert.Equal("leadói szöveg", placer.Message);

        // Mindkettő ugyanarra a rendelésre és napra mutat.
        Assert.All(notifications, n =>
        {
            Assert.Equal(5, n.RelatedMenuOrderId);
            Assert.Equal(new DateOnly(2026, 8, 20), n.RelatedDate);
            Assert.Equal(NotificationType.MenuCancelled, n.Type);
        });
    }

    [Fact]
    public void A_cancellation_with_credit_is_CreditIssued_for_the_owner_but_MenuCancelled_for_the_placer()
    {
        // 01 §3.3 — a jóváírás a tulajdonosé; a leadónak ez puszta lemondás.
        using var db = dbFactory.CreateDbContext();
        var credit = new CreditEntry
        {
            UserId = Owner,
            AmountHuf = 1400,
            Kind = CreditEntryKind.CancellationCredit,
            CreatedByUserId = Admin,
            RemainingHuf = 1400,
        };

        sut.NotifyOrderCancelled(db, Order(placedBy: Colleague), credit, OwnerText, PlacerText, performedByUserId: Admin, NowUtc);

        var notifications = db.UserNotifications.Local.ToList();
        Assert.Equal(NotificationType.CreditIssued, notifications.Single(n => n.UserId == Owner).Type);
        Assert.Equal(NotificationType.MenuCancelled, notifications.Single(n => n.UserId == Colleague).Type);
    }

    [Fact]
    public void A_cancellation_without_credit_is_MenuCancelled_for_both()
    {
        using var db = dbFactory.CreateDbContext();

        sut.NotifyOrderCancelled(db, Order(placedBy: Colleague), credit: null, OwnerText, PlacerText, performedByUserId: Admin, NowUtc);

        Assert.All(db.UserNotifications.Local, n => Assert.Equal(NotificationType.MenuCancelled, n.Type));
        Assert.Equal(2, db.UserNotifications.Local.Count);
    }

    [Fact]
    public void The_placer_is_skipped_when_they_performed_the_action_themselves()
    {
        var notifications = Notify(Order(placedBy: Colleague), performedBy: Colleague);

        var single = Assert.Single(notifications);
        Assert.Equal(Owner, single.UserId);
    }
}
