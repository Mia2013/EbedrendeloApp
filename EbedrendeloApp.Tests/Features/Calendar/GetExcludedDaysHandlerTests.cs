using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Features.Calendar.GetExcludedDays;
using EbedrendeloApp.Tests.TestSupport;

namespace EbedrendeloApp.Tests.Features.Calendar;

public class GetExcludedDaysHandlerTests : IDisposable
{
    private static readonly DateOnly Before = new(2026, 8, 10);
    private static readonly DateOnly InsideEarly = new(2026, 8, 17);
    private static readonly DateOnly InsideLate = new(2026, 8, 24);
    private static readonly DateOnly After = new(2026, 9, 7);

    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly GetExcludedDaysHandler sut;

    public GetExcludedDaysHandlerTests() => sut = new GetExcludedDaysHandler(dbFactory);

    public void Dispose() => dbFactory.Dispose();

    [Fact]
    public async Task Returns_only_the_days_inside_the_range_ordered_by_date()
    {
        await SeedAsync();

        // A tartomány mindkét vége zárt: a határnapok bent vannak.
        var days = await sut.Handle(new GetExcludedDaysQuery(InsideEarly, InsideLate), CancellationToken.None);

        Assert.Equal([InsideEarly, InsideLate], days.Select(d => d.Date));
    }

    [Fact]
    public async Task Resolves_the_creator_display_name_and_keeps_the_reason()
    {
        await SeedAsync();

        var days = await sut.Handle(new GetExcludedDaysQuery(Before, After), CancellationToken.None);

        var first = days.First(d => d.Date == InsideEarly);
        Assert.Equal("Karbantartás", first.Reason);
        Assert.Equal("Nagy Admin", first.CreatedByDisplayName);
    }

    [Fact]
    public async Task Returns_an_empty_list_when_nothing_falls_into_the_range()
    {
        await SeedAsync();

        var days = await sut.Handle(
            new GetExcludedDaysQuery(new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 31)), CancellationToken.None);

        Assert.Empty(days);
    }

    private async Task SeedAsync()
    {
        await using var db = dbFactory.CreateDbContext();

        var role = new Role { Name = "Admin" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();

        var admin = new User { UserId = 1, UserName = "admin", VezetekNev = "Nagy", KeresztNev = "Admin", RoleId = role.Id };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        db.ExcludedDays.AddRange(
            new ExcludedDay { Date = Before, Reason = "Korábbi", CreatedByUserId = admin.Id },
            new ExcludedDay { Date = InsideEarly, Reason = "Karbantartás", CreatedByUserId = admin.Id },
            new ExcludedDay { Date = InsideLate, Reason = "Céges rendezvény", CreatedByUserId = admin.Id },
            new ExcludedDay { Date = After, Reason = "Későbbi", CreatedByUserId = admin.Id });
        await db.SaveChangesAsync();
    }
}
