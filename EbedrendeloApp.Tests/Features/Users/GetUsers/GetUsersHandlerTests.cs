using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Features.Users.GetUsers;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.Features.Users.GetUsers;

public class GetUsersHandlerTests : IDisposable
{
    private readonly SqliteDbContextFactory dbFactory = new();

    public void Dispose() => dbFactory.Dispose();

    private GetUsersHandler CreateHandler() => new(dbFactory);

    private async Task<Role> SeedRoleAsync()
    {
        await using var db = dbFactory.CreateDbContext();
        var role = await db.Roles.FirstOrDefaultAsync();
        if (role is not null)
        {
            return role;
        }

        role = new Role { Name = "User" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role;
    }

    private async Task SeedUserAsync(int userNumber, string vezetekNev, string keresztNev, string? igazgatosag = null, string? osztaly = null)
    {
        var role = await SeedRoleAsync();
        await using var db = dbFactory.CreateDbContext();
        db.Users.Add(new User
        {
            UserId = userNumber,
            UserName = $"u{userNumber}",
            VezetekNev = vezetekNev,
            KeresztNev = keresztNev,
            Igazgatosag = igazgatosag,
            Osztaly = osztaly,
            RoleId = role.Id,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Returns_empty_list_when_no_users_exist()
    {
        var result = await CreateHandler().Handle(new GetUsersQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task Orders_users_by_vezeteknev_then_keresztnev()
    {
        await SeedUserAsync(1, "Tóth", "Eszter");
        await SeedUserAsync(2, "Kovács", "Béla");
        await SeedUserAsync(3, "Kovács", "Anna");

        var result = await CreateHandler().Handle(new GetUsersQuery(), CancellationToken.None);

        Assert.Equal(["Kovács Anna", "Kovács Béla", "Tóth Eszter"], result.Value!.Select(u => u.DisplayName));
    }

    [Fact]
    public async Task Maps_userid_username_role_and_department_fields()
    {
        await SeedUserAsync(42, "Nagy", "Anna", igazgatosag: "Gyártás", osztaly: "1. üzem");

        var result = await CreateHandler().Handle(new GetUsersQuery(), CancellationToken.None);

        var user = Assert.Single(result.Value!);
        Assert.Equal(42, user.UserId);
        Assert.Equal("u42", user.UserName);
        Assert.Equal("User", user.RoleName);
        Assert.Equal("Gyártás", user.Igazgatosag);
        Assert.Equal("1. üzem", user.Osztaly);
    }

    [Fact]
    public async Task Includes_users_with_null_igazgatosag_and_osztaly()
    {
        await SeedUserAsync(1, "Szabó", "Péter");

        var result = await CreateHandler().Handle(new GetUsersQuery(), CancellationToken.None);

        var user = Assert.Single(result.Value!);
        Assert.Null(user.Igazgatosag);
        Assert.Null(user.Osztaly);
    }
}
