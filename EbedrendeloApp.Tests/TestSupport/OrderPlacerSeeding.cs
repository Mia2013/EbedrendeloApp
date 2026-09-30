using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Tests.TestSupport;

/// <summary>
/// AC 8.1.3 tesztjeihez: a rendelést egy kolléga adta le a tulajdonos nevében. Felvesz egy „kollega"
/// felhasználót (az első szerepkörrel), a rendelés leadójának beállítja, és visszaadja az azonosítóját.
/// </summary>
public static class OrderPlacerSeeding
{
    public static async Task<int> AssignColleagueAsPlacerAsync(this IDbContextFactory<EbedrendeloDbContext> dbFactory, int orderId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var colleague = new User { UserId = 3, UserName = "kollega", RoleId = (await db.Roles.FirstAsync()).Id };
        db.Users.Add(colleague);
        await db.SaveChangesAsync();

        var order = await db.MenuOrders.SingleAsync(o => o.Id == orderId);
        order.PlacedByUserId = colleague.Id;
        await db.SaveChangesAsync();
        return colleague.Id;
    }
}
