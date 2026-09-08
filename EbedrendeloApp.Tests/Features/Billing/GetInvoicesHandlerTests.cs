using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Billing.GetInvoices;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Billing;

public class GetInvoicesHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly GetInvoicesHandler sut;

    public GetInvoicesHandlerTests() => sut = new GetInvoicesHandler(dbFactory);

    public void Dispose() => dbFactory.Dispose();

    private async Task<(int period1Id, int period2Id, int user1Id, int user2Id)> SeedAsync()
    {
        await using var db = dbFactory.CreateDbContext();

        var period1 = new OrderingPeriod { Name = "Augusztus", StartDate = new DateOnly(2026, 8, 1), EndDate = new DateOnly(2026, 8, 31), OrderDeadline = new DateTime(2026, 7, 15, 10, 0, 0) };
        var period2 = new OrderingPeriod { Name = "Szeptember", StartDate = new DateOnly(2026, 9, 1), EndDate = new DateOnly(2026, 9, 30), OrderDeadline = new DateTime(2026, 8, 15, 10, 0, 0) };
        db.OrderingPeriods.AddRange(period1, period2);

        var role = new Role { Name = "User" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user1 = new User { UserId = 1, UserName = "u1", VezetekNev = "Kis", KeresztNev = "Anna", RoleId = role.Id };
        var user2 = new User { UserId = 2, UserName = "u2", VezetekNev = "Nagy", KeresztNev = "Béla", RoleId = role.Id };
        db.Users.AddRange(user1, user2);
        await db.SaveChangesAsync();

        var invoice1 = new PeriodInvoice
        {
            UserId = user1.Id, OrderingPeriodId = period1.Id, SequenceNumber = 1, GrossHuf = 1400,
            CreditAppliedHuf = 0, PayableHuf = 1400, IsPaid = true,
            GeneratedAtUtc = new DateTime(2026, 7, 20, 9, 0, 0),
        };
        var invoice2 = new PeriodInvoice
        {
            UserId = user2.Id, OrderingPeriodId = period2.Id, SequenceNumber = 1, GrossHuf = 2800,
            CreditAppliedHuf = 300, PayableHuf = 2500, IsPaid = false,
            GeneratedAtUtc = new DateTime(2026, 8, 20, 9, 0, 0),
        };
        db.PeriodInvoices.AddRange(invoice1, invoice2);

        var dish = new MenuDish { Kind = MenuDishKind.Leves, Name = "Gulyásleves" };
        db.MenuDishes.Add(dish);
        await db.SaveChangesAsync();

        var menu = new DailyMenu { Date = new DateOnly(2026, 9, 10), IsPublished = true };
        menu.Variants.Add(new MenuVariant { DailyMenuId = 0, Code = "A", SoupName = "Gulyásleves", SoupDishId = dish.Id, SortOrder = 0 });
        db.DailyMenus.Add(menu);
        await db.SaveChangesAsync();

        // A napszám a számlához kötött rendelésekből jön — user2 számlája 2 napot fedez.
        var variantId = menu.Variants[0].Id;
        db.MenuOrders.AddRange(
            new MenuOrder
            {
                UserId = user2.Id, Date = new DateOnly(2026, 9, 10), OrderingPeriodId = period2.Id, MenuVariantId = variantId,
                PriceHuf = 1400, Status = OrderStatus.Active, PlacedByUserId = user2.Id, PeriodInvoiceId = invoice2.Id,
            },
            new MenuOrder
            {
                UserId = user2.Id, Date = new DateOnly(2026, 9, 11), OrderingPeriodId = period2.Id, MenuVariantId = variantId,
                PriceHuf = 1400, Status = OrderStatus.Active, PlacedByUserId = user2.Id, PeriodInvoiceId = invoice2.Id,
            });
        await db.SaveChangesAsync();

        return (period1.Id, period2.Id, user1.Id, user2.Id);
    }

    [Fact]
    public async Task Filters_by_ordering_period_and_paid_status()
    {
        var (_, period2Id, user1Id, user2Id) = await SeedAsync();

        var all = await sut.Handle(new GetInvoicesQuery(null, null), CancellationToken.None);
        Assert.Equal(2, all.Value!.Count);

        var period2Only = await sut.Handle(new GetInvoicesQuery(period2Id, null), CancellationToken.None);
        var period2Invoice = Assert.Single(period2Only.Value!);
        Assert.Equal(user2Id, period2Invoice.UserId);
        Assert.Equal("Nagy Béla", period2Invoice.UserDisplayName);
        Assert.Equal("Szeptember", period2Invoice.PeriodName);
        Assert.Equal(1, period2Invoice.SequenceNumber);
        Assert.Equal(2, period2Invoice.DayCount);
        Assert.Equal(2800, period2Invoice.GrossHuf);
        Assert.Equal(300, period2Invoice.CreditAppliedHuf);
        Assert.Equal(2500, period2Invoice.PayableHuf);

        var unpaidOnly = await sut.Handle(new GetInvoicesQuery(null, false), CancellationToken.None);
        Assert.Equal(user2Id, Assert.Single(unpaidOnly.Value!).UserId);

        var paidOnly = await sut.Handle(new GetInvoicesQuery(null, true), CancellationToken.None);
        Assert.Equal(user1Id, Assert.Single(paidOnly.Value!).UserId);
    }
}
