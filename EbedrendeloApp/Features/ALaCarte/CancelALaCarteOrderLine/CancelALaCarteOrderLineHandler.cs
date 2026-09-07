using EbedrendeloApp.Common.ALaCarte;
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
    public async Task<Result> Handle(CancelALaCarteOrderLineCommand request, CancellationToken cancellationToken)
    {
        var today = clock.Today;

        // A közös napi kapu (hétvége / kizárt nap / határidő) a rendeléssel megosztva él, lásd
        // ALaCarteOrderingGate. A határidő-ellenőrzés itt szándékosan lejjebb, a tétel megtalálása
        // után fut — lásd az ottani megjegyzést.
        var workingDay = ALaCarteOrderingGate.CheckWorkingDay(today, workingDayCalculator);
        if (!workingDay.IsSuccess)
        {
            return workingDay;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var dayNotExcluded = await ALaCarteOrderingGate.CheckDayNotExcludedAsync(db, today, cancellationToken);
        if (!dayNotExcluded.IsSuccess)
        {
            return dayNotExcluded;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // A tétel-létezés ellenőrzése megelőzi a határidő-ellenőrzést: ha a felhasználó ma nem is
        // rendelte meg ezt a tételt, azt akkor is NotFound-nak kell jeleznünk, ha a határidő már
        // lejárt — különben a hibaüzenet félrevezető lenne ("lejárt a módosítási határidő" ahelyett,
        // hogy "ezt nem is rendelted").
        var order = await db.ALaCarteOrders
            .Include(o => o.Lines).ThenInclude(l => l.ALaCarteDailyOffer)
            .FirstOrDefaultAsync(o => o.UserId == request.UserId && o.Date == today, cancellationToken);

        var line = order?.Lines.FirstOrDefault(l => l.ALaCarteDailyOffer!.ALaCarteItemId == request.ALaCarteItemId);
        if (line is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(ErrorCodes.NotFound, "Ezt a tételt ma nem rendelted.");
        }

        var settings = await db.AppSettings.FirstAsync(cancellationToken);
        if (ALaCarteOrderingGate.IsPastDeadline(settings, clock.LocalNow))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(ErrorCodes.DeadlinePassed, "A mai à la carte rendelési határidő lejárt, a rendelés már nem módosítható.");
        }

        // A foglaláshoz szimmetrikus, atomikus visszaadás, ugyanúgy egy köteges (itt egy soros)
        // ExecuteUpdateAsync-cel, mint a PlaceALaCarteOrderHandler foglalása. Az affected-row számot
        // ellenőrizzük — ha 0 (az OrderedCount már 0 volt, miközben a sor még létezett), az egy valós
        // adatinkonzisztencia, amit nem szabad csendben elnyelni, hanem hibaként kell jeleznünk.
        var released = await db.ALaCarteDailyOffers
            .Where(o => o.Id == line.ALaCarteDailyOfferId && o.OrderedCount > 0)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.OrderedCount, o => o.OrderedCount - 1), cancellationToken);

        if (released != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(ErrorCodes.NotFound, "A foglalt készlet nem volt visszaadható — próbáld újra.");
        }

        db.ALaCarteOrderLines.Remove(line);
        order!.TotalHuf -= line.UnitPriceHuf;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Ritka versenyhelyzet: két párhuzamos visszavonás (pl. két böngészőfülben) ugyanarra a
            // sorra — az egyik már törölte, mire ez ide ért. A tranzakció miatt a fenti
            // ExecuteUpdateAsync-es visszaadás is visszagördül, tehát a készlet nem duplázódik/vész el.
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(ErrorCodes.NotFound, "Időközben már visszavontad ezt a tételt.");
        }

        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
