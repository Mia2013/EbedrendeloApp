using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.ALaCarte.DeactivateALaCarteItem;

public sealed class DeactivateALaCarteItemHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<DeactivateALaCarteItemCommand, Result>
{
    public async Task<Result> Handle(DeactivateALaCarteItemCommand request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var item = await db.ALaCarteItems.FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);
        if (item is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "A tétel nem található.");
        }

        item.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
