using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Billing.GetMyBalance;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Billing.GetMyBalance;

public class GetMyBalanceHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();

    public void Dispose() => dbFactory.Dispose();

    private GetMyBalanceHandler CreateHandler() => new(dbFactory);

    private async Task<int> SeedUserAsync()
    {
        await using var db = dbFactory.CreateDbContext();
        var role = new Role { Name = "User" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var user = new User { UserId = 1, UserName = "u1", RoleId = role.Id };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task AddCreditEntryAsync(int userId, int amountHuf, int remainingHuf, CreditEntryKind kind)
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
    public async Task Returns_zero_for_a_user_with_no_credit_entries()
    {
        var userId = await SeedUserAsync();

        var result = await CreateHandler().Handle(new GetMyBalanceQuery(userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public async Task Sums_remaining_amounts_from_multiple_cancellation_credits()
    {
        var userId = await SeedUserAsync();
        await AddCreditEntryAsync(userId, 1400, 1400, CreditEntryKind.CancellationCredit);
        await AddCreditEntryAsync(userId, 1200, 1200, CreditEntryKind.CancellationCredit);

        var result = await CreateHandler().Handle(new GetMyBalanceQuery(userId), CancellationToken.None);

        Assert.Equal(2600, result.Value);
    }

    [Fact]
    public async Task Excludes_revoked_amount_after_a_credit_is_revoked()
    {
        var userId = await SeedUserAsync();
        // Mirrors CreditService.RevokeCredit: the original entry's RemainingHuf is zeroed, and the new
        // CreditRevoked row is created with RemainingHuf = 0 too — neither contributes to the balance.
        await AddCreditEntryAsync(userId, 1400, 0, CreditEntryKind.CancellationCredit);
        await AddCreditEntryAsync(userId, -1400, 0, CreditEntryKind.CreditRevoked);

        var result = await CreateHandler().Handle(new GetMyBalanceQuery(userId), CancellationToken.None);

        Assert.Equal(0, result.Value);
    }

    [Fact]
    public async Task Includes_manual_adjustment_entries_in_the_balance()
    {
        var userId = await SeedUserAsync();
        await AddCreditEntryAsync(userId, 500, 500, CreditEntryKind.ManualAdjustment);

        var result = await CreateHandler().Handle(new GetMyBalanceQuery(userId), CancellationToken.None);

        Assert.Equal(500, result.Value);
    }

    [Fact]
    public async Task Only_sums_entries_for_the_requesting_user()
    {
        var userId = await SeedUserAsync();
        await using (var db = dbFactory.CreateDbContext())
        {
            var otherUser = new User { UserId = 2, UserName = "u2", RoleId = (await db.Roles.SingleAsync()).Id };
            db.Users.Add(otherUser);
            await db.SaveChangesAsync();
            db.CreditEntries.Add(new CreditEntry
            {
                UserId = otherUser.Id,
                AmountHuf = 9000,
                RemainingHuf = 9000,
                Kind = CreditEntryKind.CancellationCredit,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = otherUser.Id,
            });
            await db.SaveChangesAsync();
        }
        await AddCreditEntryAsync(userId, 700, 700, CreditEntryKind.CancellationCredit);

        var result = await CreateHandler().Handle(new GetMyBalanceQuery(userId), CancellationToken.None);

        Assert.Equal(700, result.Value);
    }
}
