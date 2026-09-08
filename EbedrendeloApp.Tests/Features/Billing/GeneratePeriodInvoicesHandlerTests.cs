using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Billing.GeneratePeriodInvoices;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Billing;

public class GeneratePeriodInvoicesHandlerTests : IDisposable
{
    private static readonly DateOnly OrderDate = new(2026, 9, 10);
    private static readonly DateOnly SecondOrderDate = new(2026, 9, 11);

    private readonly SqliteDbContextFactory dbFactory = new();
    private int periodId;
    private int variantId;
    private int adminId;
    private int nextWorkerUserId = 100;

    public void Dispose() => dbFactory.Dispose();

    private GeneratePeriodInvoicesHandler CreateHandler(DateTime nowLocal)
        => new(dbFactory, new FixedAppClock(nowLocal), new CreditService(), new NotificationService());

    private async Task SeedPeriodAsync(DateTime orderDeadline)
    {
        await using var db = dbFactory.CreateDbContext();

        var period = new OrderingPeriod
        {
            Name = "Szeptember",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 9, 30),
            OrderDeadline = orderDeadline,
        };
        db.OrderingPeriods.Add(period);

        var role = new Role { Name = "User" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var admin = new User { UserId = 999, UserName = "admin", RoleId = role.Id };
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        adminId = admin.Id;

        var dish = new MenuDish { Kind = MenuDishKind.Leves, Name = "Gulyásleves" };
        db.MenuDishes.Add(dish);
        await db.SaveChangesAsync();

        var menu = new DailyMenu { Date = OrderDate, IsPublished = true };
        menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Gulyásleves", SoupDishId = dish.Id, SortOrder = 0 });
        db.DailyMenus.Add(menu);
        await db.SaveChangesAsync();

        variantId = menu.Variants[0].Id;
        periodId = period.Id;
    }

    private async Task<int> SeedUserAsync()
    {
        await using var db = dbFactory.CreateDbContext();
        var role = await db.Roles.FirstAsync();
        var user = new User { UserId = nextWorkerUserId, UserName = $"worker{nextWorkerUserId++}", RoleId = role.Id };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task SeedMenuOrderAsync(int userId, int priceHuf, DateOnly? date = null)
    {
        await using var db = dbFactory.CreateDbContext();
        db.MenuOrders.Add(new MenuOrder
        {
            UserId = userId,
            Date = date ?? OrderDate,
            OrderingPeriodId = periodId,
            MenuVariantId = variantId,
            PriceHuf = priceHuf,
            Status = OrderStatus.Active,
            PlacedByUserId = userId,
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedALaCarteOrderAsync(int userId, int totalHuf)
    {
        await using var db = dbFactory.CreateDbContext();
        db.ALaCarteOrders.Add(new ALaCarteOrder
        {
            UserId = userId,
            Date = OrderDate,
            OrderingPeriodId = periodId,
            PlacedByUserId = userId,
            TotalHuf = totalHuf,
        });
        await db.SaveChangesAsync();
    }

    private async Task<int> SeedCreditAsync(int userId, int amountHuf, DateTime createdAtUtc)
    {
        await using var db = dbFactory.CreateDbContext();
        var entry = new CreditEntry
        {
            UserId = userId,
            AmountHuf = amountHuf,
            Kind = CreditEntryKind.CancellationCredit,
            CreatedAtUtc = createdAtUtc,
            CreatedByUserId = userId,
            RemainingHuf = amountHuf,
        };
        db.CreditEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry.Id;
    }

    [Fact]
    public async Task Generates_a_base_invoice_from_the_periods_menu_orders()
    {
        await SeedPeriodAsync(new DateTime(2026, 8, 15, 10, 0, 0));
        var userId = await SeedUserAsync();
        await SeedMenuOrderAsync(userId, 1400);
        await SeedMenuOrderAsync(userId, 1400, SecondOrderDate);

        var sut = CreateHandler(new DateTime(2026, 8, 20, 9, 0, 0));
        var result = await sut.Handle(new GeneratePeriodInvoicesCommand(periodId, adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var invoice = Assert.Single(result.Value!.Generated);
        Assert.Equal(1, invoice.SequenceNumber);
        Assert.Equal(2, invoice.DayCount);
        Assert.Equal(2800, invoice.GrossHuf);
        Assert.Equal(0, invoice.CreditAppliedHuf);
        Assert.Equal(2800, invoice.PayableHuf);

        await using var db = dbFactory.CreateDbContext();
        var persisted = await db.PeriodInvoices.SingleAsync(i => i.UserId == userId);
        Assert.Equal(periodId, persisted.OrderingPeriodId);
        Assert.Equal(2800, persisted.GrossHuf);
        Assert.False(persisted.IsPaid);
    }

    [Fact]
    public async Task Stamps_the_invoice_id_on_every_billed_order()
    {
        await SeedPeriodAsync(new DateTime(2026, 8, 15, 10, 0, 0));
        var userId = await SeedUserAsync();
        await SeedMenuOrderAsync(userId, 1400);

        var sut = CreateHandler(new DateTime(2026, 8, 20, 9, 0, 0));
        var result = await sut.Handle(new GeneratePeriodInvoicesCommand(periodId, adminId), CancellationToken.None);

        var invoiceId = Assert.Single(result.Value!.Generated).InvoiceId;

        await using var db = dbFactory.CreateDbContext();
        var order = await db.MenuOrders.SingleAsync(o => o.UserId == userId);
        Assert.Equal(invoiceId, order.PeriodInvoiceId);
    }

    [Fact]
    public async Task Ignores_alacarte_orders_entirely()
    {
        // Az à la carte-ot a dolgozó aznap fizeti, nem az időszaki számlán — a számla csak a
        // menürendelésekről szól, akkor is, ha az időszakhoz tartozik à la carte forgalom.
        await SeedPeriodAsync(new DateTime(2026, 8, 15, 10, 0, 0));
        var userId = await SeedUserAsync();
        await SeedMenuOrderAsync(userId, 1400);
        await SeedALaCarteOrderAsync(userId, 800);

        var sut = CreateHandler(new DateTime(2026, 8, 20, 9, 0, 0));
        var result = await sut.Handle(new GeneratePeriodInvoicesCommand(periodId, adminId), CancellationToken.None);

        var invoice = Assert.Single(result.Value!.Generated);
        Assert.Equal(1400, invoice.GrossHuf);
        Assert.Equal(1400, invoice.PayableHuf);
    }

    [Fact]
    public async Task A_user_with_only_alacarte_orders_gets_no_invoice()
    {
        await SeedPeriodAsync(new DateTime(2026, 8, 15, 10, 0, 0));
        var userId = await SeedUserAsync();
        await SeedALaCarteOrderAsync(userId, 800);

        var sut = CreateHandler(new DateTime(2026, 8, 20, 9, 0, 0));
        var result = await sut.Handle(new GeneratePeriodInvoicesCommand(periodId, adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Generated);
    }

    [Fact]
    public async Task Rerunning_bills_only_the_days_ordered_since_the_previous_invoice()
    {
        await SeedPeriodAsync(new DateTime(2026, 8, 15, 10, 0, 0));
        var userId = await SeedUserAsync();
        await SeedMenuOrderAsync(userId, 1400);

        var sut = CreateHandler(new DateTime(2026, 8, 20, 9, 0, 0));
        await sut.Handle(new GeneratePeriodInvoicesCommand(periodId, adminId), CancellationToken.None);

        // B-fázisú pótrendelés a számlázás után.
        await SeedMenuOrderAsync(userId, 1400, SecondOrderDate);

        var second = await sut.Handle(new GeneratePeriodInvoicesCommand(periodId, adminId), CancellationToken.None);

        var supplementary = Assert.Single(second.Value!.Generated);
        Assert.Equal(2, supplementary.SequenceNumber);
        Assert.Equal(1, supplementary.DayCount);
        Assert.Equal(1400, supplementary.GrossHuf); // csak az új nap, nem a teljes időszak újra

        await using var db = dbFactory.CreateDbContext();
        Assert.Equal(2, await db.PeriodInvoices.CountAsync(i => i.UserId == userId));
    }

    [Fact]
    public async Task Rerunning_with_nothing_new_generates_no_invoice()
    {
        await SeedPeriodAsync(new DateTime(2026, 8, 15, 10, 0, 0));
        var userId = await SeedUserAsync();
        await SeedMenuOrderAsync(userId, 1400);

        var sut = CreateHandler(new DateTime(2026, 8, 20, 9, 0, 0));
        await sut.Handle(new GeneratePeriodInvoicesCommand(periodId, adminId), CancellationToken.None);
        var second = await sut.Handle(new GeneratePeriodInvoicesCommand(periodId, adminId), CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Empty(second.Value!.Generated);

        await using var db = dbFactory.CreateDbContext();
        Assert.Equal(1, await db.PeriodInvoices.CountAsync(i => i.UserId == userId));
    }

    [Fact]
    public async Task Bills_only_the_user_with_uninvoiced_days()
    {
        await SeedPeriodAsync(new DateTime(2026, 8, 15, 10, 0, 0));
        var earlyUserId = await SeedUserAsync();
        var lateUserId = await SeedUserAsync();
        await SeedMenuOrderAsync(earlyUserId, 1400);

        var sut = CreateHandler(new DateTime(2026, 8, 20, 9, 0, 0));
        await sut.Handle(new GeneratePeriodInvoicesCommand(periodId, adminId), CancellationToken.None);

        await SeedMenuOrderAsync(lateUserId, 1400);
        var second = await sut.Handle(new GeneratePeriodInvoicesCommand(periodId, adminId), CancellationToken.None);

        var generated = Assert.Single(second.Value!.Generated);
        Assert.Equal(lateUserId, generated.UserId);
        Assert.Equal(1, generated.SequenceNumber); // neki ez az első számlája
    }

    [Fact]
    public async Task Applies_credit_capped_at_the_invoices_gross()
    {
        await SeedPeriodAsync(new DateTime(2026, 8, 15, 10, 0, 0));
        var userId = await SeedUserAsync();
        await SeedMenuOrderAsync(userId, 1400);
        await SeedCreditAsync(userId, 2000, new DateTime(2026, 8, 1, 8, 0, 0));

        var sut = CreateHandler(new DateTime(2026, 8, 20, 9, 0, 0));
        var result = await sut.Handle(new GeneratePeriodInvoicesCommand(periodId, adminId), CancellationToken.None);

        var invoice = Assert.Single(result.Value!.Generated);
        Assert.Equal(1400, invoice.CreditAppliedHuf); // a 2000-ből csak a bruttóig, nem többet
        Assert.Equal(0, invoice.PayableHuf);
    }

    [Fact]
    public async Task Leftover_credit_beyond_the_gross_rolls_over_on_the_ledger()
    {
        await SeedPeriodAsync(new DateTime(2026, 8, 15, 10, 0, 0));
        var userId = await SeedUserAsync();
        await SeedMenuOrderAsync(userId, 1400);
        var creditId = await SeedCreditAsync(userId, 2000, new DateTime(2026, 8, 1, 8, 0, 0));

        var sut = CreateHandler(new DateTime(2026, 8, 20, 9, 0, 0));
        await sut.Handle(new GeneratePeriodInvoicesCommand(periodId, adminId), CancellationToken.None);

        await using var db = dbFactory.CreateDbContext();
        var source = await db.CreditEntries.SingleAsync(c => c.Id == creditId);
        Assert.Equal(600, source.RemainingHuf); // 2000 - 1400, a következő időszakra marad

        var applied = await db.CreditEntries.SingleAsync(c => c.Kind == CreditEntryKind.CreditApplied);
        Assert.Equal(-1400, applied.AmountHuf);
        Assert.Equal(creditId, applied.ConsumesCreditEntryId);
    }

    [Fact]
    public async Task Consumes_available_credits_oldest_first()
    {
        await SeedPeriodAsync(new DateTime(2026, 8, 15, 10, 0, 0));
        var userId = await SeedUserAsync();
        await SeedMenuOrderAsync(userId, 700);
        var olderId = await SeedCreditAsync(userId, 500, new DateTime(2026, 7, 1, 8, 0, 0));
        var newerId = await SeedCreditAsync(userId, 1000, new DateTime(2026, 8, 1, 8, 0, 0));

        var sut = CreateHandler(new DateTime(2026, 8, 20, 9, 0, 0));
        await sut.Handle(new GeneratePeriodInvoicesCommand(periodId, adminId), CancellationToken.None);

        await using var db = dbFactory.CreateDbContext();
        Assert.Equal(0, (await db.CreditEntries.SingleAsync(c => c.Id == olderId)).RemainingHuf);
        Assert.Equal(800, (await db.CreditEntries.SingleAsync(c => c.Id == newerId)).RemainingHuf);
    }

    [Fact]
    public async Task A_credit_issued_the_same_moment_as_generation_is_applied_immediately()
    {
        await SeedPeriodAsync(new DateTime(2026, 8, 15, 10, 0, 0));
        var userId = await SeedUserAsync();
        await SeedMenuOrderAsync(userId, 1400);
        var generationInstant = new DateTime(2026, 8, 20, 9, 0, 0);
        await SeedCreditAsync(userId, 1400, generationInstant); // nincs EligibleFrom késleltetés

        var sut = CreateHandler(generationInstant);
        var result = await sut.Handle(new GeneratePeriodInvoicesCommand(periodId, adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1400, Assert.Single(result.Value!.Generated).CreditAppliedHuf);
    }

    [Fact]
    public async Task Rejects_generation_before_the_order_deadline_has_passed()
    {
        await SeedPeriodAsync(new DateTime(2026, 8, 25, 10, 0, 0));
        var userId = await SeedUserAsync();
        await SeedMenuOrderAsync(userId, 1400);

        var sut = CreateHandler(new DateTime(2026, 8, 20, 9, 0, 0)); // a határidő előtt
        var result = await sut.Handle(new GeneratePeriodInvoicesCommand(periodId, adminId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.OrderWindowOpen, result.ErrorCode);
    }

    [Fact]
    public async Task Notifies_only_the_users_whose_invoice_actually_applied_credit()
    {
        await SeedPeriodAsync(new DateTime(2026, 8, 15, 10, 0, 0));
        var withCreditUserId = await SeedUserAsync();
        var withoutCreditUserId = await SeedUserAsync();
        await SeedMenuOrderAsync(withCreditUserId, 1400);
        await SeedMenuOrderAsync(withoutCreditUserId, 1400);
        await SeedCreditAsync(withCreditUserId, 500, new DateTime(2026, 8, 1, 8, 0, 0));

        var sut = CreateHandler(new DateTime(2026, 8, 20, 9, 0, 0));
        await sut.Handle(new GeneratePeriodInvoicesCommand(periodId, adminId), CancellationToken.None);

        await using var db = dbFactory.CreateDbContext();
        var notification = await db.UserNotifications.SingleAsync();
        Assert.Equal(withCreditUserId, notification.UserId);
        Assert.Equal(NotificationType.CreditApplied, notification.Type);
    }
}
