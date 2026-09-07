using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Billing.GetMyBalance;

public sealed class GetMyBalanceHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetMyBalanceQuery, Result<int>>
{
    public async Task<Result<int>> Handle(GetMyBalanceQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var balance = await db.CreditEntries
            .Where(c => c.UserId == request.UserId)
            .SumAsync(c => c.RemainingHuf, cancellationToken);

        return Result.Success(balance);
    }
}
