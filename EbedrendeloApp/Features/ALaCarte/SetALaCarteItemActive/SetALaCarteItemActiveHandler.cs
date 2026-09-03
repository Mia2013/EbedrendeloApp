using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.ALaCarte.SetALaCarteItemActive;

public sealed class SetALaCarteItemActiveHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<SetALaCarteItemActiveCommand, Result>
{
    public async Task<Result> Handle(SetALaCarteItemActiveCommand request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var item = await db.ALaCarteItems.FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);
        if (item is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "A tétel nem található.");
        }

        item.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
