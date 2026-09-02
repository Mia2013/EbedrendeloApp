using System.Data;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.ALaCarte.SetDailyOffer;

public sealed class SetDailyOfferHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<SetDailyOfferCommand, Result<ALaCarteDailyOfferDto>>
{
    public async Task<Result<ALaCarteDailyOfferDto>> Handle(SetDailyOfferCommand request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var item = await db.ALaCarteItems.FirstOrDefaultAsync(i => i.Id == request.ALaCarteItemId, cancellationToken);
        if (item is null || !item.IsActive)
        {
            return Result.Failure<ALaCarteDailyOfferDto>(ErrorCodes.NotFound, "A tétel nem található, vagy már nem aktív.");
        }

        // Szerializált tranzakció, az UpsertOrderingPeriodHandler mintáját követve: a "legfeljebb egy
        // aktív Leves ajánlat naponta" (AC 4.1.4) szabály DB-indexszel nem fejezhető ki (a Category az
        // ALaCarteItem-en van, nem az ALaCarteDailyOffer-en, 01-szerver-architektura.md 11/13) — enélkül
        // a tranzakció nélkül két egyidejű hívás (két különböző Leves tételre) mindkettő átjutna az
        // alábbi ellenőrzésen.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var existing = await db.ALaCarteDailyOffers
            .FirstOrDefaultAsync(o => o.Date == request.Date && o.ALaCarteItemId == request.ALaCarteItemId, cancellationToken);

        if (item.Category == ALaCarteCategory.Leves)
        {
            // AC 4.1.4 — naponta legfeljebb egy aktív Leves ajánlat; csak akkor sértjük ezt, ha egy MÁSIK
            // Leves tételhez tartozó ajánlat is fennáll aznapra (ugyanennek a tételnek a kapacitás-
            // frissítése nem ütközik önmagával).
            var anotherSoupOffered = await db.ALaCarteDailyOffers
                .Include(o => o.ALaCarteItem)
                .AnyAsync(o => o.Date == request.Date && o.ALaCarteItemId != request.ALaCarteItemId
                               && o.ALaCarteItem!.Category == ALaCarteCategory.Leves, cancellationToken);
            if (anotherSoupOffered)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure<ALaCarteDailyOfferDto>(
                    ErrorCodes.SoupAlreadyOffered, "Erre a napra már be van állítva leves — előbb vond vissza a meglévőt.");
            }
        }
        else if (existing is not null && request.Capacity < existing.OrderedCount)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<ALaCarteDailyOfferDto>(
                ErrorCodes.CapacityBelowReserved, "A keret nem csökkenthető a már lefoglalt darabszám alá.");
        }

        // Leves kategóriánál a keret sosem kerül ellenőrzésre (AC 4.1.2/4.2.4) — a beküldött Capacity-t
        // figyelmen kívül hagyjuk, mindig korlátlanra állítjuk.
        var capacity = item.Category == ALaCarteCategory.Leves ? int.MaxValue : request.Capacity;

        ALaCarteDailyOffer offer;
        if (existing is not null)
        {
            existing.Capacity = capacity;
            offer = existing;
        }
        else
        {
            offer = new ALaCarteDailyOffer { Date = request.Date, ALaCarteItemId = request.ALaCarteItemId, Capacity = capacity, OrderedCount = 0 };
            db.ALaCarteDailyOffers.Add(offer);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success(new ALaCarteDailyOfferDto(
            offer.Id, offer.Date, item.Id, item.Name, item.Category, item.PriceHuf,
            offer.Capacity, offer.OrderedCount, offer.Capacity - offer.OrderedCount));
    }
}
