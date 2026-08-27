using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Menus.UpsertDailyMenu;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Menus;

public class UpsertDailyMenuHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly FixedAppClock clock = new(new DateTime(2026, 8, 17, 9, 0, 0));
    private readonly UpsertDailyMenuHandler sut;

    private int userId;
    private int otherUserId;
    private int adminId;

    public UpsertDailyMenuHandlerTests()
    {
        sut = new UpsertDailyMenuHandler(
            dbFactory, clock, new MenuReassignmentService(new CreditService(), new NotificationService()), new NotificationService());
    }

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Creates_new_menu_published_immediately()
    {
        await SeedUsersAsync();

        var result = await sut.Handle(
            new UpsertDailyMenuCommand(new DateOnly(2026, 8, 20), "Megjegyzés", [new MenuVariantInput("A", "Rántott hús", "sülttel", 0)], adminId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var db = dbFactory.CreateDbContext();
        var menu = await db.DailyMenus.Include(m => m.Variants).SingleAsync(m => m.Id == result.Value);
        // Nincs külön publikálás-lépés: a sikeres mentés azonnal rendelhetővé teszi a napot.
        Assert.True(menu.IsPublished);
        Assert.Single(menu.Variants);
        Assert.Equal("A", menu.Variants[0].Code);
    }

    [Fact]
    public async Task Rejects_when_the_day_is_already_closed()
    {
        var date = new DateOnly(2026, 8, 20);
        await SeedUsersAsync();
        await using (var db = dbFactory.CreateDbContext())
        {
            db.KitchenClosures.Add(new KitchenClosure { Date = date, ClosedByUserId = adminId, TotalPortions = 0 });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(
            new UpsertDailyMenuCommand(date, null, [new MenuVariantInput("A", "Menü", null, 0)], adminId),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DayClosed, result.ErrorCode);
    }

    [Fact]
    public async Task Updating_only_name_and_description_leaves_active_orders_on_their_variant()
    {
        var date = new DateOnly(2026, 8, 20);
        var (_, orderId, variantId) = await SeedMenuWithActiveOrderAsync(date, "A", "Régi név");

        var result = await sut.Handle(
            new UpsertDailyMenuCommand(date, null, [new MenuVariantInput("A", "Új név", "Új leírás", 0)], adminId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var db = dbFactory.CreateDbContext();
        var order = await db.MenuOrders.SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Active, order.Status);
        Assert.Equal(variantId, order.MenuVariantId);
        Assert.Null(order.ReassignedFromVariantCode);

        var variant = await db.MenuVariants.SingleAsync(v => v.Id == variantId);
        Assert.Equal("Új név", variant.Name);
    }

    [Fact]
    public async Task Updating_an_existing_menu_notifies_active_orderers_with_MenuChanged()
    {
        var date = new DateOnly(2026, 8, 20);
        var (_, orderId, _) = await SeedMenuWithActiveOrderAsync(date, "A", "Régi név");

        await sut.Handle(
            new UpsertDailyMenuCommand(date, null, [new MenuVariantInput("A", "Új név", null, 0)], adminId),
            CancellationToken.None);

        await using var db = dbFactory.CreateDbContext();
        var notification = await db.UserNotifications.SingleAsync(n => n.RelatedMenuOrderId == orderId);
        Assert.Equal(NotificationType.MenuChanged, notification.Type);
    }

    [Fact]
    public async Task Resaving_an_existing_menu_with_no_actual_changes_sends_no_MenuChanged_notification()
    {
        // Regression test: re-opening and re-saving an already-published day without changing anything
        // (same Note, same variant Code/Name/Description/SortOrder) must not spam every active orderer.
        var date = new DateOnly(2026, 8, 20);
        var (_, orderId, _) = await SeedMenuWithActiveOrderAsync(date, "A", "Régi név");

        var result = await sut.Handle(
            new UpsertDailyMenuCommand(date, null, [new MenuVariantInput("A", "Régi név", null, 0)], adminId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var db = dbFactory.CreateDbContext();
        Assert.False(await db.UserNotifications.AnyAsync(n => n.RelatedMenuOrderId == orderId));
    }

    [Fact]
    public async Task Creating_a_menu_sends_no_MenuChanged_notification()
    {
        await SeedUsersAsync();

        await sut.Handle(
            new UpsertDailyMenuCommand(new DateOnly(2026, 8, 20), null, [new MenuVariantInput("A", "Menü", null, 0)], adminId),
            CancellationToken.None);

        await using var db = dbFactory.CreateDbContext();
        Assert.False(await db.UserNotifications.AnyAsync());
    }

    [Fact]
    public async Task Removing_a_variant_reassigns_active_orders_to_the_remaining_one()
    {
        var date = new DateOnly(2026, 8, 20);
        var (menuId, orderId, variantAId) = await SeedMenuWithActiveOrderAsync(date, "A", "A menü");
        int variantBId;
        await using (var db = dbFactory.CreateDbContext())
        {
            var variantB = new MenuVariant { DailyMenuId = menuId, Code = "B", Name = "B menü", SortOrder = 1 };
            db.MenuVariants.Add(variantB);
            await db.SaveChangesAsync();
            variantBId = variantB.Id;
        }

        var result = await sut.Handle(
            new UpsertDailyMenuCommand(date, null, [new MenuVariantInput("B", "B menü", null, 0)], adminId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var db2 = dbFactory.CreateDbContext();
        var order = await db2.MenuOrders.SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Active, order.Status);
        Assert.Equal(variantBId, order.MenuVariantId);
        Assert.Equal("A", order.ReassignedFromVariantCode);

        var notification = await db2.UserNotifications.SingleAsync(n => n.RelatedMenuOrderId == orderId);
        Assert.Equal(NotificationType.OrderReassigned, notification.Type);

        var removedVariant = await db2.MenuVariants.SingleAsync(v => v.Id == variantAId);
        Assert.NotNull(removedVariant.RemovedAtUtc);
    }

    [Fact]
    public async Task Renaming_the_only_variants_code_reassigns_its_active_order_to_the_new_code()
    {
        // A code rename is, from the removal loop's point of view, "A" disappearing and a brand-new "D"
        // appearing in the same call. The new variant only gets a real Id after the mid-handler
        // SaveChangesAsync — this proves that flush happens before "D" is picked as the reassignment
        // target, so the order ends up reassigned rather than wrongly cancelled for "no target".
        var date = new DateOnly(2026, 8, 20);
        var (_, orderId, _) = await SeedMenuWithActiveOrderAsync(date, "A", "A menü");

        var result = await sut.Handle(
            new UpsertDailyMenuCommand(date, null, [new MenuVariantInput("D", "D menü", null, 0)], adminId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var db = dbFactory.CreateDbContext();
        var order = await db.MenuOrders.SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Active, order.Status);
        Assert.Equal("A", order.ReassignedFromVariantCode);

        var newVariant = await db.MenuVariants.SingleAsync(v => v.Id == order.MenuVariantId);
        Assert.Equal("D", newVariant.Code);
    }

    [Fact]
    public async Task Revives_a_previously_deleted_menu_on_the_same_date()
    {
        var date = new DateOnly(2026, 8, 20);
        await SeedUsersAsync();
        int menuId;
        await using (var db = dbFactory.CreateDbContext())
        {
            var menu = new DailyMenu { Date = date, IsPublished = true, RemovedAtUtc = clock.UtcNow.UtcDateTime };
            menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", Name = "Régi", SortOrder = 0, RemovedAtUtc = clock.UtcNow.UtcDateTime });
            db.DailyMenus.Add(menu);
            await db.SaveChangesAsync();
            menuId = menu.Id;
        }

        var result = await sut.Handle(
            new UpsertDailyMenuCommand(date, null, [new MenuVariantInput("A", "Új A", null, 0)], adminId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(menuId, result.Value);

        await using var db2 = dbFactory.CreateDbContext();
        var menu2 = await db2.DailyMenus.Include(m => m.Variants).SingleAsync(m => m.Id == menuId);
        Assert.Null(menu2.RemovedAtUtc);
        // A feléledt sor is azonnal publikáltnak számít — nincs külön piszkozat-állapot.
        Assert.True(menu2.IsPublished);
        var variant = Assert.Single(menu2.Variants, v => v.RemovedAtUtc == null);
        Assert.Equal("Új A", variant.Name);
    }

    [Fact]
    public async Task Saving_a_menu_with_an_unknown_dish_name_does_not_create_a_catalog_entry()
    {
        await SeedUsersAsync();

        var result = await sut.Handle(
            new UpsertDailyMenuCommand(
                new DateOnly(2026, 8, 20), null,
                [new MenuVariantInput("A", "Gulyásleves", "Rántott hús", 0, "zeller", "glutén, tojás")],
                adminId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var db = dbFactory.CreateDbContext();
        Assert.False(await db.MenuDishes.AnyAsync(d => d.Kind == MenuDishKind.Leves && d.Name == "Gulyásleves"));
        Assert.False(await db.MenuDishes.AnyAsync(d => d.Kind == MenuDishKind.Foetel && d.Name == "Rántott hús"));
    }

    [Fact]
    public async Task Resaving_a_known_dish_with_a_blank_allergen_field_keeps_the_previously_recorded_allergens()
    {
        await SeedUsersAsync();
        var date = new DateOnly(2026, 8, 20);
        await SeedDishAsync(MenuDishKind.Leves, "Gulyásleves");

        await sut.Handle(
            new UpsertDailyMenuCommand(date, null, [new MenuVariantInput("A", "Gulyásleves", null, 0, "zeller", null)], adminId),
            CancellationToken.None);

        await sut.Handle(
            new UpsertDailyMenuCommand(date, null, [new MenuVariantInput("A", "Gulyásleves", null, 0, null, null)], adminId),
            CancellationToken.None);

        await using var db = dbFactory.CreateDbContext();
        var soup = await db.MenuDishes.SingleAsync(d => d.Kind == MenuDishKind.Leves && d.Name == "Gulyásleves");
        Assert.Equal("zeller", soup.Allergens);
    }

    [Fact]
    public async Task Resaving_a_known_dish_with_new_allergens_updates_the_catalog()
    {
        await SeedUsersAsync();
        var date = new DateOnly(2026, 8, 20);
        await SeedDishAsync(MenuDishKind.Leves, "Gulyásleves");

        await sut.Handle(
            new UpsertDailyMenuCommand(date, null, [new MenuVariantInput("A", "Gulyásleves", null, 0, "zeller", null)], adminId),
            CancellationToken.None);

        await sut.Handle(
            new UpsertDailyMenuCommand(date, null, [new MenuVariantInput("A", "Gulyásleves", null, 0, "zeller, tejtermék", null)], adminId),
            CancellationToken.None);

        await using var db = dbFactory.CreateDbContext();
        var soup = await db.MenuDishes.SingleAsync(d => d.Kind == MenuDishKind.Leves && d.Name == "Gulyásleves");
        Assert.Equal("zeller, tejtermék", soup.Allergens);
    }

    [Fact]
    public async Task Saving_a_known_dish_with_nutrition_values_updates_the_catalog()
    {
        await SeedUsersAsync();
        var date = new DateOnly(2026, 8, 20);
        await SeedDishAsync(MenuDishKind.Leves, "Gulyásleves");

        await sut.Handle(
            new UpsertDailyMenuCommand(
                date, null,
                [new MenuVariantInput("A", "Gulyásleves", null, 0, SoupEnergyKcal: 108, SoupFatGrams: 1.8m, SoupSaltGrams: 0.14m)],
                adminId),
            CancellationToken.None);

        await using var db = dbFactory.CreateDbContext();
        var soup = await db.MenuDishes.SingleAsync(d => d.Kind == MenuDishKind.Leves && d.Name == "Gulyásleves");
        Assert.Equal(108, soup.EnergyKcal);
        Assert.Equal(1.8m, soup.FatGrams);
        Assert.Equal(0.14m, soup.SaltGrams);
    }

    [Fact]
    public async Task Resaving_a_known_dish_with_blank_nutrition_fields_keeps_the_previously_recorded_values()
    {
        await SeedUsersAsync();
        var date = new DateOnly(2026, 8, 20);
        await SeedDishAsync(MenuDishKind.Leves, "Gulyásleves");

        await sut.Handle(
            new UpsertDailyMenuCommand(date, null, [new MenuVariantInput("A", "Gulyásleves", null, 0, SoupEnergyKcal: 108)], adminId),
            CancellationToken.None);

        await sut.Handle(
            new UpsertDailyMenuCommand(date, null, [new MenuVariantInput("A", "Gulyásleves", null, 0)], adminId),
            CancellationToken.None);

        await using var db = dbFactory.CreateDbContext();
        var soup = await db.MenuDishes.SingleAsync(d => d.Kind == MenuDishKind.Leves && d.Name == "Gulyásleves");
        Assert.Equal(108, soup.EnergyKcal);
    }

    private async Task SeedDishAsync(MenuDishKind kind, string name)
    {
        await using var db = dbFactory.CreateDbContext();
        db.MenuDishes.Add(new MenuDish { Kind = kind, Name = name });
        await db.SaveChangesAsync();
    }

    private async Task SeedUsersAsync()
    {
        await using var db = dbFactory.CreateDbContext();
        var role = new Role { Name = "User" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { UserId = 1, UserName = "u1", RoleId = role.Id };
        var other = new User { UserId = 2, UserName = "u2", RoleId = role.Id };
        var admin = new User { UserId = 3, UserName = "admin", RoleId = role.Id };
        db.Users.AddRange(user, other, admin);
        await db.SaveChangesAsync();

        userId = user.Id;
        otherUserId = other.Id;
        adminId = admin.Id;
        _ = otherUserId;
    }

    private async Task<(int menuId, int orderId, int variantId)> SeedMenuWithActiveOrderAsync(DateOnly date, string variantCode, string variantName)
    {
        await SeedUsersAsync();

        await using var db = dbFactory.CreateDbContext();

        var period = new OrderingPeriod
        {
            Name = "Teszt időszak",
            StartDate = date.AddDays(-10),
            EndDate = date.AddDays(10),
            OrderDeadline = date.AddDays(-15).ToDateTime(new TimeOnly(10, 0)),
        };
        db.OrderingPeriods.Add(period);

        var menu = new DailyMenu { Date = date, IsPublished = true };
        menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = variantCode, Name = variantName, SortOrder = 0 });
        db.DailyMenus.Add(menu);
        await db.SaveChangesAsync();

        var order = new MenuOrder
        {
            UserId = userId,
            Date = date,
            OrderingPeriodId = period.Id,
            MenuVariantId = menu.Variants[0].Id,
            PriceHuf = 1400,
            Status = OrderStatus.Active,
            PlacedByUserId = userId,
        };
        db.MenuOrders.Add(order);
        await db.SaveChangesAsync();

        return (menu.Id, order.Id, menu.Variants[0].Id);
    }
}
