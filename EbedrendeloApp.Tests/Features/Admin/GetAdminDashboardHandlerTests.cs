using EbedrendeloApp.Common.Calendar;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Admin.GetAdminDashboard;
using EbedrendeloApp.Tests.TestSupport;

namespace EbedrendeloApp.Tests.Features.Admin;

public class GetAdminDashboardHandlerTests : IDisposable
{
    // 2026-09-10 is a Thursday; the Monday of that week is 2026-09-07.
    private static readonly DateOnly Thursday = new(2026, 9, 10);
    private static readonly DateOnly Monday = new(2026, 9, 7);
    private static readonly DateTime MorningLocal = new(2026, 9, 10, 9, 0, 0);
    private static readonly DateTime AfternoonLocal = new(2026, 9, 10, 14, 0, 0);

    private const int AdminUserId = 1;

    private readonly SqliteDbContextFactory dbFactory = new();

    // A rendelések, zárások és kizárások idegen kulccsal mutatnak a felhasználóra, ezért az 1–5
    // azonosítójú felhasználóknak létezniük kell (1 = az admin, 2–5 = dolgozók).
    public GetAdminDashboardHandlerTests()
    {
        using var db = dbFactory.CreateDbContext();
        var role = new Role { Name = "User" };
        db.Roles.Add(role);
        db.SaveChanges();

        db.Users.AddRange(Enumerable.Range(1, 5)
            .Select(i => new User { Id = i, UserId = i, UserName = $"u{i}", RoleId = role.Id }));
        db.SaveChanges();
    }

    public void Dispose() => dbFactory.Dispose();

    private GetAdminDashboardHandler CreateHandler(DateTime nowLocal)
        => new(dbFactory, new FixedAppClock(nowLocal), new WorkingDayCalculator());

    [Fact]
    public async Task Today_menu_shows_every_published_variant_with_its_active_portion_count()
    {
        SeedSettings();
        var period = SeedPeriod();
        var variants = SeedTodayMenu();
        SeedMenuOrder(variants["A"], period.Id, Thursday, userId: 2);
        SeedMenuOrder(variants["A"], period.Id, Thursday, userId: 3);
        SeedMenuOrder(variants["B"], period.Id, Thursday, userId: 4);
        SeedMenuOrder(variants["B"], period.Id, Thursday, userId: 5, status: OrderStatus.Cancelled);

        var result = await CreateHandler(MorningLocal).Handle(new GetAdminDashboardQuery(AdminUserId), CancellationToken.None);

        Assert.Equal(3, result.TodayMenuPortions);
        Assert.Collection(result.TodayMenuVariants,
            a => Assert.Equal(("A", 2), (a.Code, a.Portions)),
            b => Assert.Equal(("B", 1), (b.Code, b.Portions)),
            // A nem rendelt variáns is szerepel, 0 adaggal — ugyanaz a szabály, mint a konyhai összesítőn.
            c => Assert.Equal(("C", 0), (c.Code, c.Portions)));
        Assert.Equal(1, result.TodayCancelledPortions);
    }

    [Fact]
    public async Task Missing_daily_menus_are_counted_for_the_working_days_of_the_active_period()
    {
        SeedSettings();
        SeedPeriod(start: Thursday, end: Thursday.AddDays(6));
        SeedTodayMenu();

        var result = await CreateHandler(MorningLocal).Handle(new GetAdminDashboardQuery(AdminUserId), CancellationToken.None);

        // Csütörtöktől a következő szerdáig 5 munkanap van; a mai napra van menü, marad 4.
        Assert.Equal(4, result.MissingDailyMenuCount);
        Assert.Equal(Thursday.AddDays(1), result.FirstMissingDailyMenu);
        Assert.Contains(result.Todos, t => t.Kind == AdminTodoKind.MissingDailyMenu && t.Count == 4);
    }

    [Fact]
    public async Task Excluded_days_do_not_count_as_missing_menu_days_and_shorten_the_remaining_period()
    {
        SeedSettings();
        SeedPeriod(start: Thursday, end: Thursday.AddDays(1));
        SeedTodayMenu();

        using (var db = dbFactory.CreateDbContext())
        {
            db.ExcludedDays.Add(new ExcludedDay { Date = Thursday.AddDays(1), Reason = "Konyhai leállás", CreatedByUserId = AdminUserId });
            db.SaveChanges();
        }

        var result = await CreateHandler(MorningLocal).Handle(new GetAdminDashboardQuery(AdminUserId), CancellationToken.None);

        Assert.Equal(0, result.MissingDailyMenuCount);
        Assert.Equal(1, result.ActivePeriod!.RemainingWorkingDays);
    }

    [Fact]
    public async Task The_weekly_a_la_carte_chart_groups_the_five_categories_into_three_stacked_bands()
    {
        SeedSettings();
        var period = SeedPeriod();
        SeedALaCarteOrder(Thursday, userId: 2,
            (ALaCarteCategory.Leves, "Gulyásleves"),
            (ALaCarteCategory.Foetel, "Rántott sajt"),
            (ALaCarteCategory.Koret, "Rizs"),
            (ALaCarteCategory.Ontet, "Tartármártás"),
            (ALaCarteCategory.Desszert, "Túrógombóc"));
        SeedALaCarteOrder(Monday, userId: 3, (ALaCarteCategory.Foetel, "Cézár saláta"));
        _ = period;

        var result = await CreateHandler(AfternoonLocal).Handle(new GetAdminDashboardQuery(AdminUserId), CancellationToken.None);

        Assert.Equal(5, result.WeeklyALaCarte.Count);

        var thursday = result.WeeklyALaCarte.Single(d => d.Date == Thursday);
        Assert.Equal(2, thursday.SoupAndMainCount);
        Assert.Equal(2, thursday.SideDishCount);
        Assert.Equal(1, thursday.DessertCount);
        Assert.Equal(5, thursday.Total);

        var monday = result.WeeklyALaCarte.Single(d => d.Date == Monday);
        Assert.Equal(1, monday.SoupAndMainCount);
        Assert.Equal(0, monday.DessertCount);
    }

    [Fact]
    public async Task Today_a_la_carte_counts_the_lines_and_the_distinct_orderers()
    {
        SeedSettings();
        SeedPeriod();
        SeedALaCarteOrder(Thursday, userId: 2, (ALaCarteCategory.Foetel, "Rántott sajt"), (ALaCarteCategory.Desszert, "Túrógombóc"));
        SeedALaCarteOrder(Thursday, userId: 3, (ALaCarteCategory.Foetel, "Cézár saláta"));

        var result = await CreateHandler(AfternoonLocal).Handle(new GetAdminDashboardQuery(AdminUserId), CancellationToken.None);

        Assert.Equal(3, result.TodayALaCarteItemCount);
        Assert.Equal(2, result.TodayALaCarteUserCount);
        // A délutáni óra a 10:30-as határidő után van.
        Assert.True(result.ALaCarteDeadlinePassed);
    }

    [Fact]
    public async Task A_missing_offer_for_the_next_working_day_becomes_a_todo()
    {
        SeedSettings();
        SeedPeriod();
        SeedTodayMenu();

        var result = await CreateHandler(MorningLocal).Handle(new GetAdminDashboardQuery(AdminUserId), CancellationToken.None);

        Assert.False(result.NextWorkingDayHasALaCarteOffer);
        Assert.Contains(result.Todos, t => t.Kind == AdminTodoKind.MissingALaCarteOffer);
    }

    [Fact]
    public async Task An_offered_next_working_day_removes_that_todo()
    {
        SeedSettings();
        SeedPeriod();
        SeedTodayMenu();
        SeedOffer(Thursday.AddDays(1), "Rántott sajt", ALaCarteCategory.Foetel, capacity: 10, orderedCount: 0);

        var result = await CreateHandler(MorningLocal).Handle(new GetAdminDashboardQuery(AdminUserId), CancellationToken.None);

        Assert.True(result.NextWorkingDayHasALaCarteOffer);
        Assert.DoesNotContain(result.Todos, t => t.Kind == AdminTodoKind.MissingALaCarteOffer);
    }

    [Fact]
    public async Task Unpaid_invoices_are_summed_and_reported_as_a_todo()
    {
        SeedSettings();
        var period = SeedPeriod();

        using (var db = dbFactory.CreateDbContext())
        {
            db.PeriodInvoices.Add(NewInvoice(2, period.Id, payable: 12_000, isPaid: false));
            db.PeriodInvoices.Add(NewInvoice(3, period.Id, payable: 8_600, isPaid: false));
            db.PeriodInvoices.Add(NewInvoice(4, period.Id, payable: 5_000, isPaid: true));
            // Jóváírással teljesen fedezett: nincs mit behajtani, ezért nem számít fizetetlennek.
            db.PeriodInvoices.Add(NewInvoice(5, period.Id, payable: 0, isPaid: false));
            db.SaveChanges();
        }

        var result = await CreateHandler(MorningLocal).Handle(new GetAdminDashboardQuery(AdminUserId), CancellationToken.None);

        Assert.Equal(2, result.UnpaidInvoiceCount);
        Assert.Equal(20_600, result.UnpaidInvoiceTotalHuf);
        Assert.Contains(result.Todos, t => t.Kind == AdminTodoKind.UnpaidInvoices && t.AmountHuf == 20_600);
    }

    [Fact]
    public async Task A_closed_kitchen_day_removes_the_closing_todo()
    {
        SeedSettings();
        var period = SeedPeriod();
        var variants = SeedTodayMenu();
        SeedMenuOrder(variants["A"], period.Id, Thursday, userId: 2);

        var openResult = await CreateHandler(MorningLocal).Handle(new GetAdminDashboardQuery(AdminUserId), CancellationToken.None);
        Assert.Contains(openResult.Todos, t => t.Kind == AdminTodoKind.KitchenDayOpen);

        using (var db = dbFactory.CreateDbContext())
        {
            db.KitchenClosures.Add(new KitchenClosure
            {
                Date = Thursday,
                ClosedAtUtc = new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc),
                ClosedByUserId = AdminUserId,
                TotalPortions = 1,
            });
            db.SaveChanges();
        }

        var closedResult = await CreateHandler(MorningLocal).Handle(new GetAdminDashboardQuery(AdminUserId), CancellationToken.None);

        Assert.True(closedResult.TodayClosedByKitchen);
        Assert.DoesNotContain(closedResult.Todos, t => t.Kind == AdminTodoKind.KitchenDayOpen);
    }

    [Fact]
    public async Task Tile_stats_report_the_admins_own_order_for_today()
    {
        SeedSettings();
        var period = SeedPeriod();
        var variants = SeedTodayMenu();
        SeedMenuOrder(variants["B"], period.Id, Thursday, userId: AdminUserId);
        SeedALaCarteOrder(Thursday, AdminUserId, (ALaCarteCategory.Desszert, "Túrógombóc"));

        var result = await CreateHandler(AfternoonLocal).Handle(new GetAdminDashboardQuery(AdminUserId), CancellationToken.None);

        Assert.Equal("B", result.Tiles.MyTodayVariantCode);
        Assert.Equal(1, result.Tiles.MyTodayALaCarteItemCount);
        Assert.Equal(1, result.Tiles.OpenPeriodCount);
    }

    [Fact]
    public async Task Without_a_covering_period_the_page_still_renders_from_empty_data()
    {
        SeedSettings();

        var result = await CreateHandler(MorningLocal).Handle(new GetAdminDashboardQuery(AdminUserId), CancellationToken.None);

        Assert.Null(result.ActivePeriod);
        Assert.Equal(0, result.TodayMenuPortions);
        Assert.Empty(result.TodayALaCarteOffers);
        Assert.Equal(5, result.WeeklyMenuPortions.Count);
    }

    private void SeedSettings()
    {
        using var db = dbFactory.CreateDbContext();
        db.AppSettings.Add(new AppSetting
        {
            MenuPortionHuf = 1400,
            ChangeDeadlineWorkingDays = 3,
            ChangeDeadlineLocalTime = new TimeOnly(11, 0),
            ALaCarteOrderDeadlineLocalTime = new TimeOnly(10, 30),
        });
        db.SaveChanges();
    }

    private OrderingPeriod SeedPeriod(DateOnly? start = null, DateOnly? end = null)
    {
        using var db = dbFactory.CreateDbContext();
        var period = new OrderingPeriod
        {
            Name = "2026. szeptember",
            StartDate = start ?? new DateOnly(2026, 9, 1),
            EndDate = end ?? new DateOnly(2026, 9, 30),
            OrderDeadline = new DateTime(2026, 8, 25, 11, 0, 0),
            IsOpen = true,
        };
        db.OrderingPeriods.Add(period);
        db.SaveChanges();
        return period;
    }

    /// <summary>A mai nap három publikált variánsa; a visszaadott szótár kód → variáns azonosító.</summary>
    private Dictionary<string, int> SeedTodayMenu()
    {
        using var db = dbFactory.CreateDbContext();

        var soup = new MenuDish { Name = "Húsleves", Kind = MenuDishKind.Leves };
        var main = new MenuDish { Name = "Rántott csirkemell", Kind = MenuDishKind.Foetel };
        db.MenuDishes.AddRange(soup, main);
        db.SaveChanges();

        var menu = new DailyMenu { Date = Thursday, IsPublished = true };
        db.DailyMenus.Add(menu);
        db.SaveChanges();

        var variants = new[] { "A", "B", "C" }
            .Select((code, index) => new MenuVariant
            {
                DailyMenuId = menu.Id,
                Code = code,
                SoupName = soup.Name,
                SoupDishId = soup.Id,
                MainCourseName = main.Name,
                MainCourseDishId = main.Id,
                SortOrder = index,
            })
            .ToList();

        db.MenuVariants.AddRange(variants);
        db.SaveChanges();

        return variants.ToDictionary(v => v.Code, v => v.Id);
    }

    private void SeedMenuOrder(int variantId, int periodId, DateOnly date, int userId, OrderStatus status = OrderStatus.Active)
    {
        using var db = dbFactory.CreateDbContext();
        db.MenuOrders.Add(new MenuOrder
        {
            UserId = userId,
            Date = date,
            OrderingPeriodId = periodId,
            MenuVariantId = variantId,
            PriceHuf = 1400,
            Status = status,
            PlacedByUserId = userId,
            PlacedAtUtc = new DateTime(2026, 8, 20, 8, 0, 0, DateTimeKind.Utc),
        });
        db.SaveChanges();
    }

    private void SeedOffer(DateOnly date, string itemName, ALaCarteCategory category, int capacity, int orderedCount)
    {
        using var db = dbFactory.CreateDbContext();
        var item = new ALaCarteItem { Name = itemName, Category = category, PriceHuf = 1490 };
        db.ALaCarteItems.Add(item);
        db.SaveChanges();

        db.ALaCarteDailyOffers.Add(new ALaCarteDailyOffer
        {
            Date = date,
            ALaCarteItemId = item.Id,
            Capacity = capacity,
            OrderedCount = orderedCount,
        });
        db.SaveChanges();
    }

    private void SeedALaCarteOrder(DateOnly date, int userId, params (ALaCarteCategory Category, string Name)[] lines)
    {
        using var db = dbFactory.CreateDbContext();

        var period = db.OrderingPeriods.First();
        var order = new ALaCarteOrder
        {
            UserId = userId,
            Date = date,
            OrderingPeriodId = period.Id,
            PlacedByUserId = userId,
            PlacedAtUtc = new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc),
            TotalHuf = lines.Length * 1000,
        };
        db.ALaCarteOrders.Add(order);
        db.SaveChanges();

        foreach (var line in lines)
        {
            var item = new ALaCarteItem { Name = line.Name, Category = line.Category, PriceHuf = 1000 };
            db.ALaCarteItems.Add(item);
            db.SaveChanges();

            var offer = new ALaCarteDailyOffer { Date = date, ALaCarteItemId = item.Id, Capacity = 10, OrderedCount = 1 };
            db.ALaCarteDailyOffers.Add(offer);
            db.SaveChanges();

            db.ALaCarteOrderLines.Add(new ALaCarteOrderLine
            {
                ALaCarteOrderId = order.Id,
                ALaCarteDailyOfferId = offer.Id,
                ItemNameSnapshot = line.Name,
                CategorySnapshot = line.Category,
                UnitPriceHuf = 1000,
            });
            db.SaveChanges();
        }
    }

    private static PeriodInvoice NewInvoice(int userId, int periodId, int payable, bool isPaid) => new()
    {
        UserId = userId,
        OrderingPeriodId = periodId,
        SequenceNumber = 1,
        GrossHuf = payable,
        CreditAppliedHuf = 0,
        PayableHuf = payable,
        IsPaid = isPaid,
        GeneratedAtUtc = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
    };
}
