using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Services;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Billing.AddManualCredit;

public sealed class AddManualCreditHandler(
    IDbContextFactory<EbedrendeloDbContext> dbFactory,
    IAppClock clock,
    ICreditService creditService,
    INotificationService notificationService)
    : IRequestHandler<AddManualCreditCommand, Result<int>>
{
    public async Task<Result<int>> Handle(AddManualCreditCommand request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // CreditEntry.UserId is a Restrict FK — without this guard, an unknown TargetUserId would
        // surface as a raw DbUpdateException at SaveChangesAsync instead of a friendly Result.Failure.
        if (!await db.Users.AnyAsync(u => u.Id == request.TargetUserId, cancellationToken))
        {
            return Result.Failure<int>(ErrorCodes.NotFound, "A felhasználó nem található.");
        }

        var nowUtc = clock.UtcNow.UtcDateTime;
        var entry = creditService.IssueManualCredit(db, request.TargetUserId, request.AmountHuf, request.PerformedByUserId, nowUtc, request.Note);

        notificationService.Notify(
            db,
            request.TargetUserId,
            NotificationType.CreditIssued,
            "Jóváírás érkezett",
            $"{request.AmountHuf} Ft jóváírás került a menü-egyenlegedhez. Indoklás: {request.Note}",
            nowUtc);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success(entry.Id);
    }
}
