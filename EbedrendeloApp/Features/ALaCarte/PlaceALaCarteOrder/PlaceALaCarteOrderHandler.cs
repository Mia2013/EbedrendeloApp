using EbedrendeloApp.Common.Calendar;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Common.Time;
using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.ALaCarte.PlaceALaCarteOrder;

public sealed class PlaceALaCarteOrderHandler(
    IDbContextFactory<EbedrendeloDbContext> dbFactory,
    IAppClock clock,
    IWorkingDayCalculator workingDayCalculator)
    : IRequestHandler<PlaceALaCarteOrderCommand, Result<PlacedALaCarteOrderLinesDto>>
{
    private static readonly IReadOnlySet<DateOnly> EmptyExcludedSet = new HashSet<DateOnly>();

    public async Task<Result<PlacedALaCarteOrderLinesDto>> Handle(PlaceALaCarteOrderCommand request, CancellationToken cancellationToken)
    {
        var today = clock.Today;

        if (!workingDayCalculator.IsWorkingDay(today, EmptyExcludedSet))
        {
            return Result.Failure<PlacedALaCarteOrderLinesDto>(ErrorCodes.NotWorkingDay, "Ma nem munkanap.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        if (await db.ExcludedDays.AnyAsync(e => e.Date == today, cancellationToken))
        {
            return Result.Failure<PlacedALaCarteOrderLinesDto>(ErrorCodes.DayExcluded, "Erre a napra nincs rendelés.");
        }

        var settings = await db.AppSettings.FirstAsync(cancellationToken);
        if (clock.LocalNow.TimeOfDay > settings.ALaCarteOrderDeadlineLocalTime.ToTimeSpan())
        {
            return Result.Failure<PlacedALaCarteOrderLinesDto>(ErrorCodes.DeadlinePassed, "A mai à la carte rendelési határidő lejárt.");
        }

        var period = await db.OrderingPeriods
            .FirstOrDefaultAsync(p => p.StartDate <= today && today <= p.EndDate, cancellationToken);
        if (period is null)
        {
            return Result.Failure<PlacedALaCarteOrderLinesDto>(ErrorCodes.OutsidePeriod, "Ma nincs érvényes rendelési időszak.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var offers = await db.ALaCarteDailyOffers
            .Include(o => o.ALaCarteItem)
            .Where(o => o.Date == today && request.ALaCarteItemIds.Contains(o.ALaCarteItemId))
            .ToListAsync(cancellationToken);

        if (offers.Count != request.ALaCarteItemIds.Count)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<PlacedALaCarteOrderLinesDto>(ErrorCodes.OfferNotFound, "A kért tételek egy része ma nem elérhető.");
        }

        if (offers.Any(o => o.ALaCarteItem!.Category == ALaCarteCategory.Leves))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<PlacedALaCarteOrderLinesDto>(ErrorCodes.NotDirectlyOrderable, "A leves önállóan nem rendelhető.");
        }

        var existingOrder = await db.ALaCarteOrders
            .Include(o => o.Lines).ThenInclude(l => l.ALaCarteDailyOffer)
            .FirstOrDefaultAsync(o => o.UserId == request.UserId && o.Date == today, cancellationToken);

        if (existingOrder is not null && existingOrder.Lines.Any(l => request.ALaCarteItemIds.Contains(l.ALaCarteDailyOffer!.ALaCarteItemId)))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<PlacedALaCarteOrderLinesDto>(ErrorCodes.AlreadyOrdered, "Ezt a tételt már megrendelted ma.");
        }

        var todaySoupItem = await db.ALaCarteDailyOffers
            .Include(o => o.ALaCarteItem)
            .Where(o => o.Date == today && o.ALaCarteItem!.Category == ALaCarteCategory.Leves)
            .Select(o => o.ALaCarteItem)
            .FirstOrDefaultAsync(cancellationToken);

        // Az első feltételes (nem-üres Where-es) ExecuteUpdateAsync a kódbázisban — a 3.4. fejezet
        // dokumentált versenyhelyzet-elve: egyetlen köteges UPDATE-tel foglal minden kért tételre
        // egyszerre (nem tételenként külön kör-úttal) — csak azok a sorok inkrementálódnak, amelyeknél
        // OrderedCount < Capacity még igaz a végrehajtás pillanatában.
        var offerIds = offers.Select(o => o.Id).ToList();
        var reserved = await db.ALaCarteDailyOffers
            .Where(o => offerIds.Contains(o.Id) && o.OrderedCount < o.Capacity)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.OrderedCount, o => o.OrderedCount + 1), cancellationToken);

        if (reserved != offers.Count)
        {
            // Nem fért bele minden tétel a keretbe — mielőtt visszagörgetjük a részlegesen már
            // megtörtént foglalásokat (AC 4.2.4: nincs részleges a la carte rendelés), kiderítjük,
            // melyik tétel maradt a foglalás előtti darabszámon (az nem fért bele).
            var currentCounts = await db.ALaCarteDailyOffers
                .Where(o => offerIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, o => o.OrderedCount, cancellationToken);
            var outOfStockOffer = offers.First(o => currentCounts[o.Id] == o.OrderedCount);

            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<PlacedALaCarteOrderLinesDto>(ErrorCodes.OutOfStock, $"{outOfStockOffer.ALaCarteItem!.Name} elfogyott.");
        }

        var order = existingOrder;
        if (order is null)
        {
            order = new ALaCarteOrder
            {
                UserId = request.UserId,
                Date = today,
                OrderingPeriodId = period.Id,
                PlacedAtUtc = clock.UtcNow.UtcDateTime,
                PlacedByUserId = request.UserId,
                TotalHuf = 0,
            };
            db.ALaCarteOrders.Add(order);
        }

        var placedLines = new List<PlacedALaCarteOrderLineDto>();
        foreach (var offer in offers)
        {
            var item = offer.ALaCarteItem!;
            var includesSoup = item.Category == ALaCarteCategory.Foetel && todaySoupItem is not null;
            var unitPriceHuf = includesSoup ? item.PriceHuf + todaySoupItem!.PriceHuf : item.PriceHuf;

            order.Lines.Add(new ALaCarteOrderLine
            {
                ALaCarteOrderId = order.Id,
                ALaCarteDailyOfferId = offer.Id,
                ItemNameSnapshot = item.Name,
                CategorySnapshot = item.Category,
                UnitPriceHuf = unitPriceHuf,
                IncludesSoup = includesSoup,
            });

            placedLines.Add(new PlacedALaCarteOrderLineDto(item.Id, item.Name, item.Category, unitPriceHuf, includesSoup));
        }

        order.TotalHuf += placedLines.Sum(l => l.UnitPriceHuf);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Ritka versenyhelyzet: (ALaCarteOrderId, ALaCarteDailyOfferId) unique index — két
            // párhuzamos hívás mindkettő átjutott a fenti in-memory AlreadyOrdered-ellenőrzésen, mielőtt
            // bármelyik mentett volna. A tranzakció miatt az ExecuteUpdateAsync-es foglalások is
            // visszagördülnek, tehát a kapacitás nem vész el.
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<PlacedALaCarteOrderLinesDto>(
                ErrorCodes.AlreadyOrdered, "Időközben valaki más rendelést adott le ugyanerre a napra — próbáld újra.");
        }

        await transaction.CommitAsync(cancellationToken);

        return Result.Success(new PlacedALaCarteOrderLinesDto(placedLines, placedLines.Sum(l => l.UnitPriceHuf)));
    }
}
