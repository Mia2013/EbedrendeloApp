using EbedrendeloApp.Common.Calendar;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Features.Calendar.GetUncoveredWorkdays;
using EbedrendeloApp.Tests.TestSupport;

namespace EbedrendeloApp.Tests.Features.Calendar;

public class GetUncoveredWorkdaysHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly GetUncoveredWorkdaysHandler sut;

    public GetUncoveredWorkdaysHandlerTests() => sut = new GetUncoveredWorkdaysHandler(dbFactory, new WorkingDayCalculator());

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Returns_the_weekday_gap_between_two_periods_and_excludes_weekends()
    {
        // Period 1: 2026-08-05 (Wed) .. 2026-08-19 (Wed). Period 2: 2026-08-24 (Mon) .. 2026-09-05.
        // Gap weekdays: 2026-08-20 (Thu), 2026-08-21 (Fri). 08-22/23 are weekend and must not appear.
        using (var db = dbFactory.CreateDbContext())
        {
            db.OrderingPeriods.AddRange(
                new OrderingPeriod { Name = "P1", StartDate = new DateOnly(2026, 8, 5), EndDate = new DateOnly(2026, 8, 19), OrderDeadline = new DateTime(2026, 7, 26, 10, 0, 0) },
                new OrderingPeriod { Name = "P2", StartDate = new DateOnly(2026, 8, 24), EndDate = new DateOnly(2026, 9, 5), OrderDeadline = new DateTime(2026, 8, 14, 10, 0, 0) });
            await db.SaveChangesAsync();
        }

        // Query range starts at P1.StartDate so the only gap in range is the one between the periods.
        var result = await sut.Handle(new GetUncoveredWorkdaysQuery(new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 31)), CancellationToken.None);

        Assert.Equal([new DateOnly(2026, 8, 20), new DateOnly(2026, 8, 21)], result);
    }

    [Fact]
    public async Task Returns_empty_when_the_whole_range_is_covered()
    {
        using (var db = dbFactory.CreateDbContext())
        {
            db.OrderingPeriods.Add(new OrderingPeriod { Name = "P1", StartDate = new DateOnly(2026, 8, 1), EndDate = new DateOnly(2026, 8, 31), OrderDeadline = new DateTime(2026, 7, 20, 10, 0, 0) });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetUncoveredWorkdaysQuery(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Does_not_report_a_weekday_that_is_already_excluded()
    {
        // 2026-08-20 (Thu) falls in the gap between the two periods AND is explicitly excluded —
        // it must show up via GetExcludedDaysQuery only, not also as "uncovered", or the
        // "Nem rendelhető napok" page would list it twice.
        using (var db = dbFactory.CreateDbContext())
        {
            db.OrderingPeriods.AddRange(
                new OrderingPeriod { Name = "P1", StartDate = new DateOnly(2026, 8, 5), EndDate = new DateOnly(2026, 8, 19), OrderDeadline = new DateTime(2026, 7, 26, 10, 0, 0) },
                new OrderingPeriod { Name = "P2", StartDate = new DateOnly(2026, 8, 24), EndDate = new DateOnly(2026, 9, 5), OrderDeadline = new DateTime(2026, 8, 14, 10, 0, 0) });

            var role = new Role { Name = "Admin" };
            db.Roles.Add(role);
            await db.SaveChangesAsync();

            var admin = new User { UserId = 1, UserName = "admin", RoleId = role.Id };
            db.Users.Add(admin);
            await db.SaveChangesAsync();

            db.ExcludedDays.Add(new ExcludedDay { Date = new DateOnly(2026, 8, 20), Reason = "Karbantartás", CreatedByUserId = admin.Id });
            await db.SaveChangesAsync();
        }

        var result = await sut.Handle(new GetUncoveredWorkdaysQuery(new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 31)), CancellationToken.None);

        Assert.Equal([new DateOnly(2026, 8, 21)], result);
    }
}
