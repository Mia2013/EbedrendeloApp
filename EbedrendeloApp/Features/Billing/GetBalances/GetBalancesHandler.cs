using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Billing.GetBalances;

public sealed class GetBalancesHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetBalancesQuery, Result<IReadOnlyList<UserBalanceDto>>>
{
    public async Task<Result<IReadOnlyList<UserBalanceDto>>> Handle(GetBalancesQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Sima Sum-aggregátum, nincs "melyik sor legyen a nyertes" rendezési függőség (szemben
        // RemoveExcludedDayHandler kliens-oldali csoportosításával) — EF Core ezt megbízhatóan SQL
        // GROUP BY/SUM-ra fordítja.
        var balances = await db.CreditEntries
            .GroupBy(c => c.UserId)
            .Select(g => new { UserId = g.Key, BalanceHuf = g.Sum(c => c.RemainingHuf) })
            .Where(x => x.BalanceHuf != 0)
            .ToListAsync(cancellationToken);

        var userIds = balances.Select(b => b.UserId).ToList();
        var users = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var result = balances
            .Select(b =>
            {
                var user = users[b.UserId];
                return new UserBalanceDto(b.UserId, $"{user.VezetekNev} {user.KeresztNev}".Trim(), user.Igazgatosag, user.Osztaly, b.BalanceHuf);
            })
            .OrderBy(d => d.DisplayName, StringComparer.Ordinal)
            .ToList();

        return Result.Success<IReadOnlyList<UserBalanceDto>>(result);
    }
}
