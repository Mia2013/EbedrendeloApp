using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Billing.GetBalances;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Billing.GetBalances;

public class GetBalancesHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();

    public void Dispose() => dbFactory.Dispose();

    private GetBalancesHandler CreateHandler() => new(dbFactory);

    private async Task<int> SeedUserAsync(int userNumber, string vezetekNev, string keresztNev, string? igazgatosag = null, string? osztaly = null)
    {
        await using var db = dbFactory.CreateDbContext();
        var role = await db.Roles.FirstOrDefaultAsync();
        if (role is null)
        {
            role = new Role { Name = "User" };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
        }

        var user = new User
        {
            UserId = userNumber,
            UserName = $"u{userNumber}",
            VezetekNev = vezetekNev,
            KeresztNev = keresztNev,
            Igazgatosag = igazgatosag,
            Osztaly = osztaly,
            RoleId = role.Id,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task AddCreditEntryAsync(int userId, int amountHuf, int remainingHuf, CreditEntryKind kind = CreditEntryKind.CancellationCredit)
    {
        await using var db = dbFactory.CreateDbContext();
        db.CreditEntries.Add(new CreditEntry
        {
            UserId = userId,
            AmountHuf = amountHuf,
            RemainingHuf = remainingHuf,
            Kind = kind,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Returns_empty_list_when_no_credit_entries_exist()
    {
        var result = await CreateHandler().Handle(new GetBalancesQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task Excludes_users_whose_balance_is_exactly_zero()
    {
        var zeroUserId = await SeedUserAsync(1, "Nulla", "Elemér");
        await AddCreditEntryAsync(zeroUserId, 1400, 0);
        await AddCreditEntryAsync(zeroUserId, -1400, 0, CreditEntryKind.CreditRevoked);

        var result = await CreateHandler().Handle(new GetBalancesQuery(), CancellationToken.None);

        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task Includes_a_user_with_a_negative_balance()
    {
        var userId = await SeedUserAsync(1, "Anomália", "Adminisztrátor");
        await AddCreditEntryAsync(userId, -100, -100);

        var result = await CreateHandler().Handle(new GetBalancesQuery(), CancellationToken.None);

        var entry = Assert.Single(result.Value!);
        Assert.Equal(-100, entry.BalanceHuf);
    }

    [Fact]
    public async Task Sums_multiple_credit_entries_per_user()
    {
        var userId = await SeedUserAsync(1, "Kovács", "János");
        await AddCreditEntryAsync(userId, 1400, 1400);
        await AddCreditEntryAsync(userId, 800, 800, CreditEntryKind.ManualAdjustment);

        var result = await CreateHandler().Handle(new GetBalancesQuery(), CancellationToken.None);

        var entry = Assert.Single(result.Value!);
        Assert.Equal(2200, entry.BalanceHuf);
    }

    [Fact]
    public async Task Orders_results_by_display_name()
    {
        var toth = await SeedUserAsync(1, "Tóth", "Eszter");
        var kovacs = await SeedUserAsync(2, "Kovács", "János");
        await AddCreditEntryAsync(toth, 500, 500);
        await AddCreditEntryAsync(kovacs, 500, 500);

        var result = await CreateHandler().Handle(new GetBalancesQuery(), CancellationToken.None);

        Assert.Equal(["Kovács János", "Tóth Eszter"], result.Value!.Select(b => b.DisplayName));
    }

    [Fact]
    public async Task Includes_igazgatosag_and_osztaly_in_the_result()
    {
        var userId = await SeedUserAsync(1, "Varga", "Balázs", igazgatosag: "Logisztika", osztaly: "Raktár");
        await AddCreditEntryAsync(userId, 500, 500);

        var result = await CreateHandler().Handle(new GetBalancesQuery(), CancellationToken.None);

        var entry = Assert.Single(result.Value!);
        Assert.Equal("Logisztika", entry.Igazgatosag);
        Assert.Equal("Raktár", entry.Osztaly);
    }
}
