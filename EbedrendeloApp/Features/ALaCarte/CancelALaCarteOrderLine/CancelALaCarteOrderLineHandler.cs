using EbedrendeloApp.Common.Calendar;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.ALaCarte.CancelALaCarteOrderLine;

public sealed class CancelALaCarteOrderLineHandler(
    IDbContextFactory<EbedrendeloDbContext> dbFactory,
    IAppClock clock,
    IWorkingDayCalculator workingDayCalculator)
    : IRequestHandler<CancelALaCarteOrderLineCommand, Result>
{
    private static readonly IReadOnlySet<DateOnly> EmptyExcludedSet = new HashSet<DateOnly>();

    public async Task<Result> Handle(CancelALaCarteOrderLineCommand request, CancellationToken cancellationToken)
    {
        var today = clock.Today;

        if (!workingDayCalculator.IsWorkingDay(today, EmptyExcludedSet))
        {
            return Result.Failure(ErrorCodes.NotWorkingDay, "Ma nem munkanap.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        if (await db.ExcludedDays.AnyAsync(e => e.Date == today, cancellationToken))
        {
            return Result.Failure(ErrorCodes.DayExcluded, "Erre a napra nincs rendelés.");
        }

        var settings = await db.AppSettings.FirstAsync(cancellationToken);
        if (clock.LocalNow.TimeOfDay > settings.ALaCarteOrderDeadlineLocalTime.ToTimeSpan())
        {
            return Result.Failure(ErrorCodes.DeadlinePassed, "A mai à la carte rendelési határidő lejárt, a rendelés már nem módosítható.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var order = await db.ALaCarteOrders
            .Include(o => o.Lines).ThenInclude(l => l.ALaCarteDailyOffer)
            .FirstOrDefaultAsync(o => o.UserId == request.UserId && o.Date == today, cancellationToken);

        var line = order?.Lines.FirstOrDefault(l => l.ALaCarteDailyOffer!.ALaCarteItemId == request.ALaCarteItemId);
        if (line is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(ErrorCodes.NotFound, "Ezt a tételt ma nem rendelted.");
        }

        // Az OrderedCount > 0 csak biztonsági háló — a foglaláshoz szimmetrikus, atomikus
        // visszaadás, ugyanúgy egy köteges (itt egy soros) ExecuteUpdateAsync-cel, mint a
        // PlaceALaCarteOrderHandler foglalása.
        await db.ALaCarteDailyOffers
            .Where(o => o.Id == line.ALaCarteDailyOfferId && o.OrderedCount > 0)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.OrderedCount, o => o.OrderedCount - 1), cancellationToken);

        db.ALaCarteOrderLines.Remove(line);
        order!.TotalHuf -= line.UnitPriceHuf;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
