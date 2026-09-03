using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Billing.GetMyCreditLedger;
using EbedrendeloApp.Features.Orders;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Billing.GetMyCreditLedger;

public class GetMyCreditLedgerHandlerTests : IDisposable
{
    private static readonly DateOnly OrderDate = new(2026, 8, 20);

    private readonly SqliteDbContextFactory dbFactory = new();

    public void Dispose() => dbFactory.Dispose();

    private GetMyCreditLedgerHandler CreateHandler() => new(dbFactory);

    private async Task<int> SeedUserAsync(int userNumber = 1, string? vezetekNev = null)
    {
        await using var db = dbFactory.CreateDbContext();
        var role = await db.Roles.FirstOrDefaultAsync() ?? new Role { Name = "User" };
        if (role.Id == 0)
        {
            db.Roles.Add(role);
            await db.SaveChangesAsync();
        }

        var user = new User { UserId = userNumber, UserName = $"u{userNumber}", VezetekNev = vezetekNev, RoleId = role.Id };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>Seeds a cancelled MenuOrder (with a real variant) and its matching CancellationCredit —
    /// everything GetMyCreditLedgerQuery's SourceMenuOrderId join needs to resolve a date + variant name.</summary>
    private async Task<(int orderId, int creditId, string variantName)> SeedCancelledOrderWithCreditAsync(int userId, int amountHuf = 1400)
    {
        await using var db = dbFactory.CreateDbContext();

        var period = new OrderingPeriod
        {
            Name = "Teszt időszak",
            StartDate = OrderDate,
            EndDate = OrderDate,
            OrderDeadline = DateTime.UtcNow,
        };
        db.OrderingPeriods.Add(period);

        var dish = new MenuDish { Kind = MenuDishKind.Leves, Name = "Gulyásleves" };
        db.MenuDishes.Add(dish);
        await db.SaveChangesAsync();

        var menu = new DailyMenu { Date = OrderDate, IsPublished = true };
        menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Gulyásleves", SoupDishId = dish.Id, SortOrder = 0 });
        db.DailyMenus.Add(menu);
        await db.SaveChangesAsync();

        var variant = menu.Variants[0];
        var order = new MenuOrder
        {
            UserId = userId,
            Date = OrderDate,
            OrderingPeriodId = period.Id,
            MenuVariantId = variant.Id,
            PriceHuf = amountHuf,
            Status = OrderStatus.Cancelled,
            PlacedByUserId = userId,
            CancelledAtUtc = DateTime.UtcNow,
            CancelledByUserId = userId,
            CancellationReason = CancellationReason.ByUser,
        };
        db.MenuOrders.Add(order);
        await db.SaveChangesAsync();

        var credit = new CreditEntry
        {
            UserId = userId,
            AmountHuf = amountHuf,
            RemainingHuf = amountHuf,
            Kind = CreditEntryKind.CancellationCredit,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
            SourceMenuOrderId = order.Id,
        };
        db.CreditEntries.Add(credit);
        await db.SaveChangesAsync();

        return (order.Id, credit.Id, VariantDisplayName.Combine(variant.SoupName, variant.MainCourseName));
    }

    [Fact]
    public async Task Returns_empty_list_for_a_user_with_no_ledger_entries()
    {
        var userId = await SeedUserAsync();

        var result = await CreateHandler().Handle(new GetMyCreditLedgerQuery(userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task Returns_cancellation_credit_with_source_order_date_and_variant_name()
    {
        var userId = await SeedUserAsync();
        var (orderId, _, variantName) = await SeedCancelledOrderWithCreditAsync(userId);

        var result = await CreateHandler().Handle(new GetMyCreditLedgerQuery(userId), CancellationToken.None);

        var entry = Assert.Single(result.Value!);
        Assert.Equal(CreditEntryKind.CancellationCredit, entry.Kind);
        Assert.Equal(orderId, entry.SourceMenuOrderId);
        Assert.Equal(OrderDate, entry.SourceOrderDate);
        Assert.Equal(variantName, entry.SourceOrderVariantName);
    }

    [Fact]
    public async Task Returns_credit_revoked_entry_with_its_note_and_creator()
    {
        var userId = await SeedUserAsync();
        var adminId = await SeedUserAsync(userNumber: 2, vezetekNev: "Admin Teszt");
        var (_, creditId, _) = await SeedCancelledOrderWithCreditAsync(userId);

        await using (var db = dbFactory.CreateDbContext())
        {
            var original = await db.CreditEntries.SingleAsync(c => c.Id == creditId);
            db.CreditEntries.Add(new CreditEntry
            {
                UserId = userId,
                AmountHuf = -original.AmountHuf,
                RemainingHuf = 0,
                Kind = CreditEntryKind.CreditRevoked,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = adminId,
                ConsumesCreditEntryId = original.Id,
                Note = "Kizárás visszavonva",
            });
            original.RemainingHuf = 0;
            await db.SaveChangesAsync();
        }

        var result = await CreateHandler().Handle(new GetMyCreditLedgerQuery(userId), CancellationToken.None);

        var revoked = Assert.Single(result.Value!, e => e.Kind == CreditEntryKind.CreditRevoked);
        Assert.Equal("Kizárás visszavonva", revoked.Note);
        Assert.Equal(creditId, revoked.ConsumesCreditEntryId);
        Assert.Equal(adminId, revoked.CreatedByUserId);
        Assert.Equal("Admin Teszt", revoked.CreatedByDisplayName);
        Assert.Equal(0, revoked.RemainingHuf);
    }

    [Fact]
    public async Task Returns_manual_adjustment_entry_without_a_source_order()
    {
        var userId = await SeedUserAsync();
        await using (var db = dbFactory.CreateDbContext())
        {
            db.CreditEntries.Add(new CreditEntry
            {
                UserId = userId,
                AmountHuf = 500,
                RemainingHuf = 500,
                Kind = CreditEntryKind.ManualAdjustment,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = userId,
                Note = "Konyhai üzemzavar kompenzáció",
            });
            await db.SaveChangesAsync();
        }

        var result = await CreateHandler().Handle(new GetMyCreditLedgerQuery(userId), CancellationToken.None);

        var entry = Assert.Single(result.Value!);
        Assert.Equal(CreditEntryKind.ManualAdjustment, entry.Kind);
        Assert.Null(entry.SourceMenuOrderId);
        Assert.Null(entry.SourceOrderDate);
        Assert.Null(entry.SourceOrderVariantName);
    }

    [Fact]
    public async Task Orders_entries_chronologically_by_creation_time()
    {
        var userId = await SeedUserAsync();
        var now = DateTime.UtcNow;
        await using (var db = dbFactory.CreateDbContext())
        {
            db.CreditEntries.Add(new CreditEntry { UserId = userId, AmountHuf = 300, RemainingHuf = 300, Kind = CreditEntryKind.ManualAdjustment, CreatedAtUtc = now.AddMinutes(-10), CreatedByUserId = userId, Note = "harmadik" });
            db.CreditEntries.Add(new CreditEntry { UserId = userId, AmountHuf = 100, RemainingHuf = 100, Kind = CreditEntryKind.ManualAdjustment, CreatedAtUtc = now.AddMinutes(-30), CreatedByUserId = userId, Note = "első" });
            db.CreditEntries.Add(new CreditEntry { UserId = userId, AmountHuf = 200, RemainingHuf = 200, Kind = CreditEntryKind.ManualAdjustment, CreatedAtUtc = now.AddMinutes(-20), CreatedByUserId = userId, Note = "második" });
            await db.SaveChangesAsync();
        }

        var result = await CreateHandler().Handle(new GetMyCreditLedgerQuery(userId), CancellationToken.None);

        Assert.Equal(["első", "második", "harmadik"], result.Value!.Select(e => e.Note));
    }

    [Fact]
    public async Task Only_returns_entries_for_the_requesting_user()
    {
        var userId = await SeedUserAsync();
        var otherUserId = await SeedUserAsync(userNumber: 2);
        await using (var db = dbFactory.CreateDbContext())
        {
            db.CreditEntries.Add(new CreditEntry { UserId = otherUserId, AmountHuf = 900, RemainingHuf = 900, Kind = CreditEntryKind.ManualAdjustment, CreatedAtUtc = DateTime.UtcNow, CreatedByUserId = otherUserId });
            await db.SaveChangesAsync();
        }

        var result = await CreateHandler().Handle(new GetMyCreditLedgerQuery(userId), CancellationToken.None);

        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task Surfaces_period_invoice_id_when_present()
    {
        // Epic 7 (GeneratePeriodInvoicesCommand) isn't built yet, so no handler produces this today —
        // seeded directly to prove the DTO round-trips the field for forward compatibility (AC 5.3.1).
        var userId = await SeedUserAsync();
        await using (var db = dbFactory.CreateDbContext())
        {
            var period = new OrderingPeriod { Name = "P", StartDate = OrderDate, EndDate = OrderDate, OrderDeadline = DateTime.UtcNow };
            db.OrderingPeriods.Add(period);
            await db.SaveChangesAsync();

            var invoice = new PeriodInvoice
            {
                UserId = userId,
                OrderingPeriodId = period.Id,
                MenuGrossHuf = 1400,
                ALaCarteGrossHuf = 0,
                GrossHuf = 1400,
                CreditAppliedHuf = 1400,
                MenuPayableHuf = 0,
                ALaCartePayableHuf = 0,
                PayableHuf = 0,
                GeneratedAtUtc = DateTime.UtcNow,
            };
            db.PeriodInvoices.Add(invoice);
            await db.SaveChangesAsync();

            db.CreditEntries.Add(new CreditEntry
            {
                UserId = userId,
                AmountHuf = -1400,
                RemainingHuf = 0,
                Kind = CreditEntryKind.CreditApplied,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = userId,
                PeriodInvoiceId = invoice.Id,
            });
            await db.SaveChangesAsync();
        }

        var result = await CreateHandler().Handle(new GetMyCreditLedgerQuery(userId), CancellationToken.None);

        var entry = Assert.Single(result.Value!);
        Assert.NotNull(entry.PeriodInvoiceId);
    }
}
