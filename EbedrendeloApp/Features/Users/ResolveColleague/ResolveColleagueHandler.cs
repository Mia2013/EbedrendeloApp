using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Data;
using EbedrendeloApp.Features.Users.GetUsers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Users.ResolveColleague;

public sealed class ResolveColleagueHandler(
    IDbContextFactory<EbedrendeloDbContext> dbFactory,
    ICurrentUser currentUser,
    ILogger<ResolveColleagueHandler> logger)
    : IRequestHandler<ResolveColleagueQuery, Result<UserOptionDto>>
{
    /// <summary>Ugyanaz az üzenet minden sikertelen esetre — nem árulhatja el, hogy a név, az
    /// igazgatóság vagy az osztály volt-e hibás, mert abból egyenként ki lehetne találni a hármast.</summary>
    private const string NotFoundMessage =
        "Nincs ilyen dolgozó. A rendeléshez pontosan meg kell adnod a nevét, az igazgatóságát és az osztályát.";

    public async Task<Result<UserOptionDto>> Handle(ResolveColleagueQuery request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var igazgatosag = request.Igazgatosag.Trim();
        var osztaly = request.Osztaly.Trim();

        if (name.Length == 0 || igazgatosag.Length == 0 || osztaly.Length == 0)
        {
            return Result.Failure<UserOptionDto>(ErrorCodes.NotFound, NotFoundMessage);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // A szűkítés (igazgatóság + osztály) mehet SQL-ben; a névre a teljes "Vezetéknév Keresztnév"
        // alakot kell összehasonlítani, amit itt, memóriában képzünk — egy szervezeti egység létszáma
        // ehhez bőven kicsi. A collation kis-nagybetű független, a névnél ezt explicitté tesszük.
        var candidates = await db.Users.Include(u => u.Role)
            .Where(u => u.Igazgatosag == igazgatosag && u.Osztaly == osztaly)
            .ToListAsync(cancellationToken);

        var matches = candidates
            .Where(u => string.Equals($"{u.VezetekNev} {u.KeresztNev}".Trim(), name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Két azonos nevű kolléga ugyanabban az osztályban: nem tudjuk eldönteni, melyikre gondolt,
        // és tippelni sem szabad — ugyanaz a semleges üzenet megy vissza.
        if (matches.Count != 1)
        {
            logger.LogInformation(
                "Sikertelen kolléga-azonosítás: {UserId} felhasználó {MatchCount} találatot kapott",
                currentUser.UserId, matches.Count);
            return Result.Failure<UserOptionDto>(ErrorCodes.NotFound, NotFoundMessage);
        }

        var user = matches[0];
        return Result.Success(new UserOptionDto(
            user.Id,
            user.UserName,
            user.UserId,
            $"{user.VezetekNev} {user.KeresztNev}".Trim(),
            user.Role!.Name,
            user.Igazgatosag,
            user.Osztaly));
    }
}
