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

    private async Task SeedMenuOrderAsync(int userId, int priceHuf)
    {
        await using var db = dbFactory.CreateDbContext();
        db.MenuOrders.Add(new MenuOrder
        {
            UserId = userId,
            Date = OrderDate,
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
    public async Task Generates_an_invoice_splitting_menu_and_alacarte_gross_amounts()
    {
        await SeedPeriodAsync(new DateTime(2026, 8, 15, 10, 0, 0));
        var userId = await SeedUserAsync();
        await SeedMenuOrderAsync(userId, 1400);
        await SeedALaCarteOrderAsync(userId, 800);

        var sut = CreateHandler(new DateTime(2026, 8, 20, 9, 0, 0));
        var result = await sut.Handle(new GeneratePeriodInvoicesCommand(periodId, adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var invoice = Assert.Single(result.Value!.Generated);
        Assert.Equal(1400, invoice.MenuGrossHuf);
        Assert.Equal(800, invoice.ALaCarteGrossHuf);
        Assert.Equal(0, invoice.CreditAppliedHuf);
        Assert.Equal(1400, invoice.MenuPayableHuf);
        Assert.Equal(800, invoice.ALaCartePayableHuf);
        Assert.Equal(2200, invoice.PayableHuf);
        Assert.Empty(result.Value.SkippedAlreadyInvoicedUserIds);

        await using var db = dbFactory.CreateDbContext();
        var persisted = await db.PeriodInvoices.SingleAsync(i => i.UserId == userId);
        Assert.Equal(periodId, persisted.OrderingPeriodId);
        Assert.Equal(2200, persisted.GrossHuf);
        Assert.False(persisted.IsPaid);
    }

    [Fact]
    public async Task Applies_credit_only_to_the_menu_portion_never_the_alacarte_portion()
    {
        await SeedPeriodAsync(new DateTime(2026, 8, 15, 10, 0, 0));
        var userId = await SeedUserAsync();
        await SeedMenuOrderAsync(userId, 1400);
        await SeedALaCarteOrderAsync(userId, 1000);
        await SeedCreditAsync(userId, 2000, new DateTime(2026, 8, 1, 8, 0, 0));

        var sut = CreateHandler(new DateTime(2026, 8, 20, 9, 0, 0));
        var result = await sut.Handle(new GeneratePeriodInvoicesCommand(periodId, adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var invoice = Assert.Single(result.Value!.Generated);
        Assert.Equal(1400, invoice.CreditAppliedHuf); // capped at MenuGrossHuf, not the full 2000 available
        Assert.Equal(0, invoice.MenuPayableHuf);
        Assert.Equal(1000, invoice.ALaCartePayableHuf); // untouched by the credit
        Assert.Equal(1000, invoice.PayableHuf);
    }

    [Fact]
    public async Task Leftover_credit_beyond_the_menu_gross_rolls_over_on_the_ledger()
    {
        await SeedPeriodAsync(new DateTime(2026, 8, 15, 10, 0, 0));
        var userId = await SeedUserAsync();
        await SeedMenuOrderAsync(userId, 1400);
        var creditId = await SeedCreditAsync(userId, 2000, new DateTime(2026, 8, 1, 8, 0, 0));

        var sut = CreateHandler(new DateTime(2026, 8, 20, 9, 0, 0));
        await sut.Handle(new GeneratePeriodInvoicesCommand(periodId, adminId), CancellationToken.None);

        await using var db = dbFactory.CreateDbContext();
        var source = await db.CreditEntries.SingleAsync(c => c.Id == creditId);
        Assert.Equal(600, source.RemainingHuf); // 2000 - 1400, stays on the ledger for the next period

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
        await SeedCreditAsync(userId, 1400, generationInstant); // no EligibleFrom delay — same instant is fine

        var sut = CreateHandler(generationInstant);
        var result = await sut.Handle(new GeneratePeriodInvoicesCommand(periodId, adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1400, Assert.Single(result.Value!.Generated).CreditAppliedHuf);
    }

    [Fact]
    public async Task Skips_users_who_already_have_an_invoice_for_the_period()
    {
        await SeedPeriodAsync(new DateTime(2026, 8, 15, 10, 0, 0));
        var invoicedUserId = await SeedUserAsync();
        var freshUserId = await SeedUserAsync();
        await SeedMenuOrderAsync(invoicedUserId, 1400);
        await SeedMenuOrderAsync(freshUserId, 1400);

        await using (var db = dbFactory.CreateDbContext())
        {
            db.PeriodInvoices.Add(new PeriodInvoice
            {
                UserId = invoicedUserId,
                OrderingPeriodId = periodId,
                MenuGrossHuf = 1400,
                ALaCarteGrossHuf = 0,
                GrossHuf = 1400,
                CreditAppliedHuf = 0,
                MenuPayableHuf = 1400,
                ALaCartePayableHuf = 0,
                PayableHuf = 1400,
                GeneratedAtUtc = new DateTime(2026, 8, 16, 9, 0, 0),
            });
            await db.SaveChangesAsync();
        }

        var sut = CreateHandler(new DateTime(2026, 8, 20, 9, 0, 0));
        var result = await sut.Handle(new GeneratePeriodInvoicesCommand(periodId, adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var generated = Assert.Single(result.Value!.Generated);
        Assert.Equal(freshUserId, generated.UserId);
        var skipped = Assert.Single(result.Value.SkippedAlreadyInvoicedUserIds);
        Assert.Equal(invoicedUserId, skipped);
    }

    [Fact]
    public async Task Rejects_generation_before_the_order_deadline_has_passed()
    {
        await SeedPeriodAsync(new DateTime(2026, 8, 25, 10, 0, 0));
        var userId = await SeedUserAsync();
        await SeedMenuOrderAsync(userId, 1400);

        var sut = CreateHandler(new DateTime(2026, 8, 20, 9, 0, 0)); // before OrderDeadline
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
