using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Users.GetUsers;

public sealed class GetUsersHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetUsersQuery, IReadOnlyList<UserOptionDto>>
{
    public async Task<IReadOnlyList<UserOptionDto>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var users = await db.Users.Include(u => u.Role)
            .OrderBy(u => u.VezetekNev).ThenBy(u => u.KeresztNev)
            .Select(u => new UserOptionDto(
                u.Id,
                u.UserName,
                u.UserId,
                (u.VezetekNev + " " + u.KeresztNev).Trim(),
                u.Role!.Name,
                u.Igazgatosag,
                u.Osztaly))
            .ToListAsync(cancellationToken);

        return users;
    }
}
