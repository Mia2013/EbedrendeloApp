using EbedrendeloApp.Common.Results;
using EbedrendeloApp.Data;
using EbedrendeloApp.Domain.Entities;
using EbedrendeloApp.Features.Orders;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EbedrendeloApp.Features.Billing.GetMyCreditLedger;

public sealed class GetMyCreditLedgerHandler(IDbContextFactory<EbedrendeloDbContext> dbFactory)
    : IRequestHandler<GetMyCreditLedgerQuery, Result<IReadOnlyList<CreditLedgerEntryDto>>>
{
    public async Task<Result<IReadOnlyList<CreditLedgerEntryDto>>> Handle(GetMyCreditLedgerQuery request, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Chronological (oldest first) — an append-only ledger reads as a running statement, where the
        // cause (a cancellation credit) should appear before its effect (a later revocation), AC 5.3.3.
        var entries = await db.CreditEntries
            .Where(c => c.UserId == request.UserId)
            .OrderBy(c => c.CreatedAtUtc).ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

        var orderIds = entries.Where(e => e.SourceMenuOrderId != null)
            .Select(e => e.SourceMenuOrderId!.Value).Distinct().ToList();
        var orders = await db.MenuOrders
            .Where(o => orderIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, cancellationToken);

        var variantIds = orders.Values.Select(o => o.MenuVariantId).Distinct().ToList();
        var variants = await db.MenuVariants
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        var creatorIds = entries.Select(e => e.CreatedByUserId).Distinct().ToList();
        var creatorNames = await db.Users
            .Where(u => creatorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => $"{u.VezetekNev} {u.KeresztNev}".Trim(), cancellationToken);

        var result = entries.Select(e =>
        {
            MenuOrder? order = e.SourceMenuOrderId is { } orderId ? orders.GetValueOrDefault(orderId) : null;
            var variantName = order is not null
                ? VariantDisplayName.Combine(variants[order.MenuVariantId].SoupName, variants[order.MenuVariantId].MainCourseName)
                : null;

            return new CreditLedgerEntryDto(
                e.Id,
                e.Kind,
                e.AmountHuf,
                e.RemainingHuf,
                e.CreatedAtUtc,
                e.Note,
                e.CreatedByUserId,
                creatorNames.GetValueOrDefault(e.CreatedByUserId, "Ismeretlen felhasználó"),
                e.SourceMenuOrderId,
                order?.Date,
                variantName,
                e.ConsumesCreditEntryId,
                e.PeriodInvoiceId);
        }).ToList();

        return Result.Success<IReadOnlyList<CreditLedgerEntryDto>>(result);
    }
}
