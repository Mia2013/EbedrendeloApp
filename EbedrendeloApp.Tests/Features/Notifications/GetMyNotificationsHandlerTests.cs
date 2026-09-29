using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Notifications.GetMyNotifications;
using EbedrendeloApp.Tests.TestSupport;

namespace EbedrendeloApp.Tests.Features.Notifications;

public class GetMyNotificationsHandlerTests : IDisposable
{
    // 2026-09-10 is a Thursday.
    private static readonly DateTime NowLocal = new(2026, 9, 10, 9, 0, 0);
    private static readonly DateTime BaseUtc = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly int ownerId;
    private readonly int colleagueId;
    private readonly int strangerId;

    public GetMyNotificationsHandlerTests()
    {
        using var db = dbFactory.CreateDbContext();
        var role = new Role { Name = "User" };
        db.Roles.Add(role);
        db.SaveChanges();

        var owner = new User { UserId = 1, UserName = "anna", VezetekNev = "Nagy", KeresztNev = "Anna", RoleId = role.Id };
        var colleague = new User { UserId = 2, UserName = "bela", VezetekNev = "Kiss", KeresztNev = "Béla", RoleId = role.Id };
        var stranger = new User { UserId = 3, UserName = "cecil", RoleId = role.Id };
        db.Users.AddRange(owner, colleague, stranger);
        db.SaveChanges();

        ownerId = owner.Id;
        colleagueId = colleague.Id;
        strangerId = stranger.Id;
    }

    public void Dispose() => dbFactory.Dispose();

    private GetMyNotificationsHandler CreateHandler() => new(dbFactory, new FixedAppClock(NowLocal));

    private void SeedNotification(int userId, int minutesAfterBase, bool read = false, int? relatedMenuOrderId = null, string title = "Cím")
    {
        using var db = dbFactory.CreateDbContext();
        db.UserNotifications.Add(new UserNotification
        {
            UserId = userId,
            Type = NotificationType.MenuCancelled,
            Title = title,
            Message = "Szöveg",
            RelatedDate = new DateOnly(2026, 9, 15),
            RelatedMenuOrderId = relatedMenuOrderId,
            CreatedAtUtc = BaseUtc.AddMinutes(minutesAfterBase),
            ReadAtUtc = read ? BaseUtc.AddDays(1) : null,
        });
        db.SaveChanges();
    }

    /// <summary>Egy rendelés <paramref name="ownerUserId"/> nevére, <paramref name="placedByUserId"/>
    /// leadásával — az értesítés <c>RelatedMenuOrderId</c>-jához.</summary>
    private int SeedOrder(int ownerUserId, int placedByUserId)
    {
        using var db = dbFactory.CreateDbContext();
        var period = new OrderingPeriod
        {
            Name = "2026. szeptember",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 9, 30),
            OrderDeadline = new DateTime(2026, 8, 25, 11, 0, 0),
        };
        db.OrderingPeriods.Add(period);
        var dish = new MenuDish { Kind = MenuDishKind.Leves, Name = "Húsleves" };
        db.MenuDishes.Add(dish);
        db.SaveChanges();

        var menu = new DailyMenu { Date = new DateOnly(2026, 9, 15), IsPublished = true };
        menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = dish.Name, SoupDishId = dish.Id, SortOrder = 0 });
        db.DailyMenus.Add(menu);
        db.SaveChanges();

        var order = new MenuOrder
        {
            UserId = ownerUserId,
            Date = menu.Date,
            OrderingPeriodId = period.Id,
            MenuVariantId = menu.Variants[0].Id,
            PriceHuf = 1400,
            PlacedByUserId = placedByUserId,
        };
        db.MenuOrders.Add(order);
        db.SaveChanges();
        return order.Id;
    }

    [Fact]
    public async Task Returns_only_the_callers_notifications_newest_first()
    {
        SeedNotification(ownerId, 1, title: "Régebbi");
        SeedNotification(ownerId, 2, title: "Újabb");
        SeedNotification(strangerId, 3, title: "Idegen");

        var result = await CreateHandler().Handle(new GetMyNotificationsQuery(ownerId), CancellationToken.None);

        Assert.Equal(["Újabb", "Régebbi"], result.Select(n => n.Title));
    }

    [Fact]
    public async Task Returns_at_most_the_latest_twenty()
    {
        for (var i = 0; i < 25; i++)
        {
            SeedNotification(ownerId, i, title: $"#{i}");
        }

        var result = await CreateHandler().Handle(new GetMyNotificationsQuery(ownerId), CancellationToken.None);

        Assert.Equal(GetMyNotificationsQuery.Limit, result.Count);
        Assert.Equal("#24", result[0].Title);
        Assert.Equal("#5", result[^1].Title);
    }

    [Fact]
    public async Task Unread_only_filters_out_the_read_ones_and_reports_the_read_flag()
    {
        SeedNotification(ownerId, 1, read: true, title: "Olvasott");
        SeedNotification(ownerId, 2, title: "Olvasatlan");

        var all = await CreateHandler().Handle(new GetMyNotificationsQuery(ownerId), CancellationToken.None);
        var unread = await CreateHandler().Handle(new GetMyNotificationsQuery(ownerId, UnreadOnly: true), CancellationToken.None);

        Assert.Equal([false, true], all.Select(n => n.IsRead));
        Assert.Equal("Olvasatlan", Assert.Single(unread).Title);
    }

    [Fact]
    public async Task The_placer_sees_whose_order_the_notification_is_about()
    {
        var orderId = SeedOrder(ownerUserId: ownerId, placedByUserId: colleagueId);
        SeedNotification(ownerId, 1, relatedMenuOrderId: orderId, title: "Tulajdonosé");
        SeedNotification(colleagueId, 1, relatedMenuOrderId: orderId, title: "Leadóé");

        var owner = await CreateHandler().Handle(new GetMyNotificationsQuery(ownerId), CancellationToken.None);
        var placer = await CreateHandler().Handle(new GetMyNotificationsQuery(colleagueId), CancellationToken.None);

        // A tulajdonosnak a saját rendeléséről szól — nincs „kinek a nevében".
        Assert.Null(Assert.Single(owner).OnBehalfOfName);
        Assert.Equal("Nagy Anna", Assert.Single(placer).OnBehalfOfName);
    }

    [Fact]
    public async Task Created_time_is_converted_to_local_time()
    {
        SeedNotification(ownerId, 30);

        var result = await CreateHandler().Handle(new GetMyNotificationsQuery(ownerId), CancellationToken.None);

        // A FixedAppClock a UTC-t változatlanul adja vissza helyi időként — a lényeg, hogy a handler
        // az órán keresztül számol, nem a gép időzónájával.
        Assert.Equal(new DateTime(2026, 9, 1, 8, 30, 0), Assert.Single(result).CreatedAtLocal);
    }
}
