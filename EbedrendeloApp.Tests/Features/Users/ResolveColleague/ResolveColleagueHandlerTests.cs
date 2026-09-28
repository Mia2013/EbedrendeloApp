using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Features.Users.ResolveColleague;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EbedrendeloApp.Tests.Features.Users.ResolveColleague;

public class ResolveColleagueHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();
    private readonly ResolveColleagueHandler sut;
    private int nextUserId = 1001;

    public ResolveColleagueHandlerTests()
        => sut = new ResolveColleagueHandler(
            dbFactory,
            new FakeCurrentUser(1, "Teszt Dolgozó", isAdmin: false),
            NullLogger<ResolveColleagueHandler>.Instance);

    public void Dispose() => dbFactory.Dispose();

    private async Task<int> SeedUserAsync(string vezetekNev, string keresztNev, string? igazgatosag, string? osztaly)
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
            UserId = nextUserId,
            // A UserName egyedi indexet visel — a névazonos kollégák tesztjéhez is külön kell.
            UserName = $"{vezetekNev}.{keresztNev}.{nextUserId++}".ToLowerInvariant(),
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

    [Fact]
    public async Task Resolves_a_colleague_when_all_three_fields_match()
    {
        var id = await SeedUserAsync("Kovács", "Anna", "Gyártás", "Logisztika");

        var result = await sut.Handle(new ResolveColleagueQuery("Kovács Anna", "Gyártás", "Logisztika"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(id, result.Value!.Id);
        Assert.Equal("Kovács Anna", result.Value.DisplayName);
    }

    [Fact]
    public async Task Ignores_casing_and_surrounding_whitespace()
    {
        await SeedUserAsync("Kovács", "Anna", "Gyártás", "Logisztika");

        var result = await sut.Handle(new ResolveColleagueQuery("  kovács anna ", " Gyártás ", " Logisztika "), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    // Jó név, rossz szervezeti egység — a hármasból egy elem ismerete nem elég.
    [InlineData("Kovács Anna", "Gyártás", "Pénzügy")]
    [InlineData("Kovács Anna", "Kereskedelem", "Logisztika")]
    // Részleges név nem talál: nincs "kezdődik vele" keresés, amivel végig lehetne próbálgatni.
    [InlineData("Kovács", "Gyártás", "Logisztika")]
    [InlineData("Anna", "Gyártás", "Logisztika")]
    // Üres mező sem elég.
    [InlineData("Kovács Anna", "", "Logisztika")]
    [InlineData("", "Gyártás", "Logisztika")]
    public async Task Rejects_anything_short_of_an_exact_match_on_all_three(string name, string igazgatosag, string osztaly)
    {
        await SeedUserAsync("Kovács", "Anna", "Gyártás", "Logisztika");

        var result = await sut.Handle(new ResolveColleagueQuery(name, igazgatosag, osztaly), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Uses_one_neutral_message_so_a_miss_never_reveals_which_field_was_wrong()
    {
        await SeedUserAsync("Kovács", "Anna", "Gyártás", "Logisztika");

        var wrongName = await sut.Handle(new ResolveColleagueQuery("Nagy Béla", "Gyártás", "Logisztika"), CancellationToken.None);
        var wrongUnit = await sut.Handle(new ResolveColleagueQuery("Kovács Anna", "Gyártás", "Pénzügy"), CancellationToken.None);

        Assert.Equal(wrongName.ErrorMessage, wrongUnit.ErrorMessage);
    }

    [Fact]
    public async Task Refuses_when_two_colleagues_in_the_same_unit_share_a_name()
    {
        // Nem tippelünk, melyikre gondolt — ugyanaz a semleges elutasítás megy vissza.
        await SeedUserAsync("Kovács", "Anna", "Gyártás", "Logisztika");
        await SeedUserAsync("Kovács", "Anna", "Gyártás", "Logisztika");

        var result = await sut.Handle(new ResolveColleagueQuery("Kovács Anna", "Gyártás", "Logisztika"), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }
}
