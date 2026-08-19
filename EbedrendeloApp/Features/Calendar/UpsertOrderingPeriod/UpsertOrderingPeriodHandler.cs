using System.Data;
using EbedrendeloApp.Common.Calendar;
using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Calendar.UpsertOrderingPeriod;

public sealed class UpsertOrderingPeriodHandler(
    IDbContextFactory<EbedrendeloDbContext> dbFactory,
    IWorkingDayCalculator workingDayCalculator)
    : IRequestHandler<UpsertOrderingPeriodCommand, Result<OrderingPeriodDto>>
{
    public async Task<Result<OrderingPeriodDto>> Handle(UpsertOrderingPeriodCommand request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        OrderingPeriod period;
        var hasOrders = false;

        if (request.Id is { } id)
        {
            var existing = await db.OrderingPeriods.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (existing is null)
            {
                return Result.Failure<OrderingPeriodDto>(ErrorCodes.NotFound, "Az időszak nem található.");
            }

            period = existing;
            hasOrders = await db.MenuOrders.AnyAsync(o => o.OrderingPeriodId == id, cancellationToken);

            if (hasOrders && (period.StartDate != request.StartDate || period.EndDate != request.EndDate || period.OrderDeadline != request.OrderDeadline))
            {
                return Result.Failure<OrderingPeriodDto>(
                    ErrorCodes.HasOrders,
                    "Az időszakhoz már tartozik rendelés — csak a név és a nyitva tartás módosítható.");
            }
        }
        else
        {
            period = new OrderingPeriod
            {
                Name = request.Name,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                OrderDeadline = request.OrderDeadline,
                IsOpen = request.IsOpen,
                CreatedAtUtc = DateTime.UtcNow,
            };
        }

        if (!hasOrders)
        {
            var overlaps = await db.OrderingPeriods
                .Where(p => request.Id == null || p.Id != request.Id)
                .AnyAsync(p => request.StartDate <= p.EndDate && request.EndDate >= p.StartDate, cancellationToken);

            if (overlaps)
            {
                return Result.Failure<OrderingPeriodDto>(ErrorCodes.Overlaps, "Az időszak átfedésben van egy meglévő időszakkal.");
            }

            var settings = await db.AppSettings.FirstAsync(cancellationToken);
            var excludedDates = await db.ExcludedDays
                .Where(e => e.Date <= request.StartDate && e.Date >= request.StartDate.AddDays(-60))
                .Select(e => e.Date)
                .ToHashSetAsync(cancellationToken);

            var maxDeadline = workingDayCalculator.ChangeDeadline(request.StartDate, settings, excludedDates);
            if (request.OrderDeadline > maxDeadline)
            {
                return Result.Failure<OrderingPeriodDto>(
                    ErrorCodes.DeadlinePassed,
                    $"A leadási határidő legfeljebb {maxDeadline:yyyy.MM.dd. HH:mm} lehet — ennyi a kezdőnap saját módosítási határideje.");
            }

            period.StartDate = request.StartDate;
            period.EndDate = request.EndDate;
            period.OrderDeadline = request.OrderDeadline;
        }

        period.Name = request.Name;
        period.IsOpen = request.IsOpen;

        if (request.Id is null)
        {
            db.OrderingPeriods.Add(period);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success(new OrderingPeriodDto(period.Id, period.Name, period.StartDate, period.EndDate, period.OrderDeadline, period.IsOpen, hasOrders));
    }
}
