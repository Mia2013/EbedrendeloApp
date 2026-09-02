using EbedrendeloApp.Common.Calendar;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Menus.GetTodayMenuForUser;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Menus;

public class GetTodayMenuForUserHandlerTests : IDisposable
{
    // 2026-08-17 is a Monday.
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly FixedAppClock clock = new(new DateTime(2026, 8, 17, 9, 0, 0));
    private readonly GetTodayMenuForUserHandler sut;

    private int userId;

    public GetTodayMenuForUserHandlerTests()
    {
        sut = new GetTodayMenuForUserHandler(dbFactory, clock, new WorkingDayCalculator());
    }

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Reports_not_orderable_on_a_weekend()
    {
        var weekendClock = new FixedAppClock(new DateTime(2026, 8, 15, 9, 0, 0)); // Saturday
        var handler = new GetTodayMenuForUserHandler(dbFactory, weekendClock, new WorkingDayCalculator());

        var result = await handler.Handle(new GetTodayMenuForUserQuery(1), CancellationToken.None);

        Assert.False(result.IsOrderableToday);
        Assert.Equal(ErrorCodes.NotWorkingDay, result.NotOrderableReason);
    }

    [Fact]
    public async Task Reports_not_orderable_when_today_is_excluded()
    {
        await SeedUserAsync();
        await using (var db = dbFactory.CreateDbContext())
        {
            db.ExcludedDays.Add(new ExcludedDay { Date = clock.Today, Reason = "Karbantartás", CreatedByUserId = userId });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetTodayMenuForUserQuery(userId), CancellationToken.None);

        Assert.False(result.IsOrderableToday);
        Assert.Equal(ErrorCodes.DayExcluded, result.NotOrderableReason);
    }

    [Fact]
    public async Task Reports_not_orderable_when_there_is_no_published_menu()
    {
        await SeedUserAsync();
        await using (var db = dbFactory.CreateDbContext())
        {
            // À la carte info is now computed unconditionally (AC 4.2.6), so AppSettings must exist
            // even when there's no menu to look at.
            db.AppSettings.Add(new AppSetting
            {
                MenuPortionHuf = 1400,
                ChangeDeadlineWorkingDays = 3,
                ChangeDeadlineLocalTime = new TimeOnly(11, 0),
                ALaCarteOrderDeadlineLocalTime = new TimeOnly(10, 30),
            });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetTodayMenuForUserQuery(userId), CancellationToken.None);

        Assert.False(result.IsOrderableToday);
        Assert.Equal(ErrorCodes.MenuNotPublished, result.NotOrderableReason);
    }

    [Fact]
    public async Task Includes_ala_carte_offers_even_when_todays_menu_is_not_published()
    {
        // AC 4.2.6: the à la carte ordering flow is independent of the A/B/C daily menu's publication
        // state — only the menu-selection part of the response should be empty here, not everything.
        await SeedUserAsync();
        await using (var db = dbFactory.CreateDbContext())
        {
            db.AppSettings.Add(new AppSetting
            {
                MenuPortionHuf = 1400,
                ChangeDeadlineWorkingDays = 3,
                ChangeDeadlineLocalTime = new TimeOnly(11, 0),
                ALaCarteOrderDeadlineLocalTime = new TimeOnly(10, 30),
            });

            var item = new ALaCarteItem { Name = "Rántott sertés szelet", Category = ALaCarteCategory.Foetel, PriceHuf = 1900, IsActive = true };
            db.ALaCarteItems.Add(item);
            await db.SaveChangesAsync();

            db.ALaCarteDailyOffers.Add(new ALaCarteDailyOffer { Date = clock.Today, ALaCarteItemId = item.Id, Capacity = 10, OrderedCount = 0 });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetTodayMenuForUserQuery(userId), CancellationToken.None);

        Assert.False(result.IsOrderableToday);
        Assert.Equal(ErrorCodes.MenuNotPublished, result.NotOrderableReason);
        Assert.Empty(result.Variants);
        Assert.Null(result.MySelection);
        var offer = Assert.Single(result.ALaCarteOffers);
        Assert.Equal("Rántott sertés szelet", offer.Name);
    }

    [Fact]
    public async Task Returns_variants_with_no_selection_when_the_user_has_not_ordered()
    {
        await SeedUserAsync();
        await SeedPublishedMenuAsync();

        var result = await sut.Handle(new GetTodayMenuForUserQuery(userId), CancellationToken.None);

        Assert.True(result.IsOrderableToday);
        Assert.Null(result.NotOrderableReason);
        Assert.Equal(2, result.Variants.Count);
        Assert.Null(result.MySelection);
    }

    [Fact]
    public async Task Returns_the_users_own_active_selection()
    {
        await SeedUserAsync();
        var (_, variantAId, _) = await SeedPublishedMenuAsync();
        await SeedActiveOrderAsync(variantAId);

        var result = await sut.Handle(new GetTodayMenuForUserQuery(userId), CancellationToken.None);

        Assert.NotNull(result.MySelection);
        Assert.Equal("A", result.MySelection!.VariantCode);
        Assert.Equal(1400, result.MySelection.PriceHuf);
    }

    [Fact]
    public async Task Includes_ala_carte_offers_with_free_count()
    {
        await SeedUserAsync();
        await SeedPublishedMenuAsync();
        await using (var db = dbFactory.CreateDbContext())
        {
            var item = new ALaCarteItem { Name = "Rántott sertés szelet", Category = ALaCarteCategory.Foetel, PriceHuf = 1900, IsActive = true, Allergens = "1,9" };
            db.ALaCarteItems.Add(item);
            await db.SaveChangesAsync();

            db.ALaCarteDailyOffers.Add(new ALaCarteDailyOffer { Date = clock.Today, ALaCarteItemId = item.Id, Capacity = 10, OrderedCount = 3 });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetTodayMenuForUserQuery(userId), CancellationToken.None);

        var offer = Assert.Single(result.ALaCarteOffers);
        Assert.Equal("Rántott sertés szelet", offer.Name);
        Assert.Equal(7, offer.FreeCount);
        Assert.Equal("1,9", offer.Allergens);
    }

    [Fact]
    public async Task Excludes_leves_offers_from_ala_carte_offers_list()
    {
        await SeedUserAsync();
        await SeedPublishedMenuAsync();
        await using (var db = dbFactory.CreateDbContext())
        {
            var soup = new ALaCarteItem { Name = "Csontleves", Category = ALaCarteCategory.Leves, PriceHuf = 650, IsActive = true };
            db.ALaCarteItems.Add(soup);
            await db.SaveChangesAsync();

            db.ALaCarteDailyOffers.Add(new ALaCarteDailyOffer { Date = clock.Today, ALaCarteItemId = soup.Id, Capacity = int.MaxValue, OrderedCount = 0 });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetTodayMenuForUserQuery(userId), CancellationToken.None);

        Assert.Empty(result.ALaCarteOffers);
    }

    [Fact]
    public async Task Combines_main_course_price_with_todays_soup_price()
    {
        await SeedUserAsync();
        await SeedPublishedMenuAsync();
        await using (var db = dbFactory.CreateDbContext())
        {
            var soup = new ALaCarteItem { Name = "Csontleves", Category = ALaCarteCategory.Leves, PriceHuf = 650, IsActive = true };
            var mainCourse = new ALaCarteItem { Name = "Rántott sertés szelet", Category = ALaCarteCategory.Foetel, PriceHuf = 1900, IsActive = true };
            db.ALaCarteItems.AddRange(soup, mainCourse);
            await db.SaveChangesAsync();

            db.ALaCarteDailyOffers.AddRange(
                new ALaCarteDailyOffer { Date = clock.Today, ALaCarteItemId = soup.Id, Capacity = int.MaxValue, OrderedCount = 0 },
                new ALaCarteDailyOffer { Date = clock.Today, ALaCarteItemId = mainCourse.Id, Capacity = 10, OrderedCount = 0 });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetTodayMenuForUserQuery(userId), CancellationToken.None);

        var offer = Assert.Single(result.ALaCarteOffers);
        Assert.Equal(2550, offer.PriceHuf);
        Assert.True(offer.IncludesSoup);
    }

    [Fact]
    public async Task Foetel_price_excludes_soup_when_no_leves_offer_exists_today()
    {
        await SeedUserAsync();
        await SeedPublishedMenuAsync();
        await using (var db = dbFactory.CreateDbContext())
        {
            var mainCourse = new ALaCarteItem { Name = "Rántott sertés szelet", Category = ALaCarteCategory.Foetel, PriceHuf = 1900, IsActive = true };
            db.ALaCarteItems.Add(mainCourse);
            await db.SaveChangesAsync();

            db.ALaCarteDailyOffers.Add(new ALaCarteDailyOffer { Date = clock.Today, ALaCarteItemId = mainCourse.Id, Capacity = 10, OrderedCount = 0 });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetTodayMenuForUserQuery(userId), CancellationToken.None);

        var offer = Assert.Single(result.ALaCarteOffers);
        Assert.Equal(1900, offer.PriceHuf);
        Assert.False(offer.IncludesSoup);
    }

    [Fact]
    public async Task Passes_through_ala_carte_item_nutrition_fields()
    {
        await SeedUserAsync();
        await SeedPublishedMenuAsync();
        await using (var db = dbFactory.CreateDbContext())
        {
            var item = new ALaCarteItem
            {
                Name = "Túrós derelye", Category = ALaCarteCategory.Desszert, PriceHuf = 750, IsActive = true,
                EnergyKcal = 320, FatGrams = 12, SaturatedFatGrams = 6, CarbohydrateGrams = 40, SugarGrams = 15, ProteinGrams = 8, SaltGrams = 0.5m,
            };
            db.ALaCarteItems.Add(item);
            await db.SaveChangesAsync();

            db.ALaCarteDailyOffers.Add(new ALaCarteDailyOffer { Date = clock.Today, ALaCarteItemId = item.Id, Capacity = 10, OrderedCount = 0 });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetTodayMenuForUserQuery(userId), CancellationToken.None);

        var offer = Assert.Single(result.ALaCarteOffers);
        Assert.Equal(320, offer.EnergyKcal);
        Assert.Equal(8, offer.ProteinGrams);
    }

    [Fact]
    public async Task My_ala_carte_line_reports_the_ordered_items_id()
    {
        await SeedUserAsync();
        await SeedPublishedMenuAsync();
        int itemId;
        int periodId;
        await using (var db = dbFactory.CreateDbContext())
        {
            var item = new ALaCarteItem { Name = "Hasábburgonya", Category = ALaCarteCategory.Koret, PriceHuf = 550, IsActive = true };
            db.ALaCarteItems.Add(item);
            await db.SaveChangesAsync();
            itemId = item.Id;

            var offer = new ALaCarteDailyOffer { Date = clock.Today, ALaCarteItemId = item.Id, Capacity = 10, OrderedCount = 1 };
            db.ALaCarteDailyOffers.Add(offer);

            var period = new OrderingPeriod { Name = "Teszt időszak", StartDate = clock.Today, EndDate = clock.Today, OrderDeadline = clock.Today.ToDateTime(new TimeOnly(10, 0)) };
            db.OrderingPeriods.Add(period);
            await db.SaveChangesAsync();
            periodId = period.Id;

            var order = new ALaCarteOrder { UserId = userId, Date = clock.Today, OrderingPeriodId = periodId, PlacedByUserId = userId, TotalHuf = 550 };
            order.Lines.Add(new ALaCarteOrderLine { ALaCarteOrderId = 0, ALaCarteDailyOfferId = offer.Id, ItemNameSnapshot = item.Name, CategorySnapshot = item.Category, UnitPriceHuf = 550 });
            db.ALaCarteOrders.Add(order);
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetTodayMenuForUserQuery(userId), CancellationToken.None);

        var line = Assert.Single(result.MyALaCarteOrderLines);
        Assert.Equal(itemId, line.ALaCarteItemId);
    }

    [Fact]
    public async Task Reports_the_ala_carte_deadline_and_whether_its_still_orderable()
    {
        await SeedUserAsync();
        await SeedPublishedMenuAsync(); // seeds AppSettings with ALaCarteOrderDeadlineLocalTime = 10:30

        var beforeDeadline = await sut.Handle(new GetTodayMenuForUserQuery(userId), CancellationToken.None); // clock is 09:00
        Assert.Equal(new TimeOnly(10, 30), beforeDeadline.ALaCarteOrderDeadlineLocalTime);
        Assert.True(beforeDeadline.IsALaCarteOrderableNow);

        var afterDeadlineClock = new FixedAppClock(new DateTime(2026, 8, 17, 11, 0, 0));
        var afterDeadlineHandler = new GetTodayMenuForUserHandler(dbFactory, afterDeadlineClock, new WorkingDayCalculator());
        var afterDeadline = await afterDeadlineHandler.Handle(new GetTodayMenuForUserQuery(userId), CancellationToken.None);
        Assert.False(afterDeadline.IsALaCarteOrderableNow);
    }

    [Fact]
    public async Task Includes_soup_allergens_from_the_dish_catalog()
    {
        await SeedUserAsync();
        var (_, _, soupADishId) = await SeedPublishedMenuAsync();
        await using (var db = dbFactory.CreateDbContext())
        {
            var dish = await db.MenuDishes.SingleAsync(d => d.Id == soupADishId);
            dish.Allergens = "glutén";
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetTodayMenuForUserQuery(userId), CancellationToken.None);

        var variant = result.Variants.Single(v => v.Code == "A");
        Assert.Equal("glutén", variant.SoupAllergens);
    }

    [Fact]
    public async Task Includes_soup_nutrition_from_the_dish_catalog()
    {
        await SeedUserAsync();
        var (_, _, soupADishId) = await SeedPublishedMenuAsync();
        await using (var db = dbFactory.CreateDbContext())
        {
            var dish = await db.MenuDishes.SingleAsync(d => d.Id == soupADishId);
            dish.EnergyKcal = 108;
            dish.FatGrams = 1.8m;
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetTodayMenuForUserQuery(userId), CancellationToken.None);

        var variant = result.Variants.Single(v => v.Code == "A");
        Assert.Equal(108, variant.SoupEnergyKcal);
        Assert.Equal(1.8m, variant.SoupFatGrams);
    }

    private async Task SeedUserAsync()
    {
        await using var db = dbFactory.CreateDbContext();
        var role = new Role { Name = "User" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { UserId = 1, UserName = "u1", RoleId = role.Id };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        userId = user.Id;
    }

    private async Task<(int menuId, int variantAId, int soupADishId)> SeedPublishedMenuAsync()
    {
        await using var db = dbFactory.CreateDbContext();

        if (!await db.AppSettings.AnyAsync())
        {
            db.AppSettings.Add(new AppSetting
            {
                MenuPortionHuf = 1400,
                ChangeDeadlineWorkingDays = 3,
                ChangeDeadlineLocalTime = new TimeOnly(11, 0),
                ALaCarteOrderDeadlineLocalTime = new TimeOnly(10, 30),
            });
            await db.SaveChangesAsync();
        }

        var dishA = new MenuDish { Kind = MenuDishKind.Leves, Name = "A menü" };
        var dishB = new MenuDish { Kind = MenuDishKind.Leves, Name = "B menü" };
        db.MenuDishes.AddRange(dishA, dishB);
        await db.SaveChangesAsync();

        var menu = new DailyMenu { Date = clock.Today, IsPublished = true };
        menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "A menü", SoupDishId = dishA.Id, SortOrder = 0 });
        menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "B", SoupName = "B menü", SoupDishId = dishB.Id, SortOrder = 1 });
        db.DailyMenus.Add(menu);
        await db.SaveChangesAsync();
        return (menu.Id, menu.Variants[0].Id, dishA.Id);
    }

    private async Task SeedActiveOrderAsync(int variantId)
    {
        await using var db = dbFactory.CreateDbContext();
        var period = new OrderingPeriod
        {
            Name = "Teszt időszak",
            StartDate = clock.Today.AddDays(-10),
            EndDate = clock.Today.AddDays(10),
            OrderDeadline = clock.Today.AddDays(-15).ToDateTime(new TimeOnly(10, 0)),
        };
        db.OrderingPeriods.Add(period);
        await db.SaveChangesAsync();

        db.MenuOrders.Add(new MenuOrder
        {
            UserId = userId,
            Date = clock.Today,
            OrderingPeriodId = period.Id,
            MenuVariantId = variantId,
            PriceHuf = 1400,
            Status = OrderStatus.Active,
            PlacedByUserId = userId,
        });
        await db.SaveChangesAsync();
    }
}
