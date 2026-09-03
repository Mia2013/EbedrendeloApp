using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using EbedrendeloApp.Features.Billing.AddManualCredit;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Billing.AddManualCredit;

public class AddManualCreditHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();

    public void Dispose() => dbFactory.Dispose();

    private AddManualCreditHandler CreateHandler(DateTime nowLocal)
        => new(dbFactory, new FixedAppClock(nowLocal), new CreditService(), new NotificationService());

    private async Task<int> SeedUserAsync(int userNumber)
    {
        await using var db = dbFactory.CreateDbContext();
        var role = await db.Roles.FirstOrDefaultAsync() ?? new Role { Name = "User" };
        if (role.Id == 0)
        {
            db.Roles.Add(role);
            await db.SaveChangesAsync();
        }

        var user = new User { UserId = userNumber, UserName = $"u{userNumber}", RoleId = role.Id };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task Adds_a_manual_credit_entry_and_updates_the_balance()
    {
        var userId = await SeedUserAsync(1);
        var adminId = await SeedUserAsync(2);
        var sut = CreateHandler(new DateTime(2026, 8, 20, 9, 0, 0));

        var result = await sut.Handle(new AddManualCreditCommand(userId, 500, "Konyhai üzemzavar kompenzáció", adminId), CancellationToken.None);

        Assert.True(result.IsSuccess);

        await using var db = dbFactory.CreateDbContext();
        var credit = await db.CreditEntries.SingleAsync(c => c.Id == result.Value);
        Assert.Equal(userId, credit.UserId);
        Assert.Equal(500, credit.AmountHuf);
        Assert.Equal(500, credit.RemainingHuf);
        Assert.Equal(CreditEntryKind.ManualAdjustment, credit.Kind);
        Assert.Equal("Konyhai üzemzavar kompenzáció", credit.Note);

        Assert.True(await db.UserNotifications.AnyAsync(n => n.UserId == userId && n.Type == NotificationType.CreditIssued));
    }

    [Fact]
    public async Task Records_the_creating_admin_and_timestamp_for_audit()
    {
        var userId = await SeedUserAsync(1);
        var adminId = await SeedUserAsync(2);
        var now = new DateTime(2026, 8, 20, 14, 30, 0);
        var sut = CreateHandler(now);

        var result = await sut.Handle(new AddManualCreditCommand(userId, 500, "Indoklás", adminId), CancellationToken.None);

        await using var db = dbFactory.CreateDbContext();
        var credit = await db.CreditEntries.SingleAsync(c => c.Id == result.Value);
        Assert.Equal(adminId, credit.CreatedByUserId);
        Assert.Equal(now, credit.CreatedAtUtc);
    }

    [Fact]
    public async Task Rejects_a_credit_for_a_nonexistent_user()
    {
        var adminId = await SeedUserAsync(1);
        var sut = CreateHandler(new DateTime(2026, 8, 20, 9, 0, 0));

        var result = await sut.Handle(new AddManualCreditCommand(TargetUserId: 999, 500, "Indoklás", adminId), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotFound, result.ErrorCode);

        await using var db = dbFactory.CreateDbContext();
        Assert.Equal(0, await db.CreditEntries.CountAsync());
    }

    [Fact]
    public async Task Does_not_affect_other_users_balances()
    {
        var userId = await SeedUserAsync(1);
        var otherUserId = await SeedUserAsync(2);
        var sut = CreateHandler(new DateTime(2026, 8, 20, 9, 0, 0));

        await sut.Handle(new AddManualCreditCommand(userId, 500, "Indoklás", otherUserId), CancellationToken.None);

        await using var db = dbFactory.CreateDbContext();
        Assert.False(await db.CreditEntries.AnyAsync(c => c.UserId == otherUserId));
    }
}
