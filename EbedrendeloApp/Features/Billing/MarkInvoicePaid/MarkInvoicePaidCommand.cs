using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Billing.MarkInvoicePaid;

/// <summary>US-7.2, AC 7.2.1 — the manual "kifizetve" flag; a flat-field update rather than an
/// append-only audit row, matching how <see cref="Domain.Entities.PeriodInvoice"/> already models
/// payment state (<c>IsPaid</c>/<c>PaidAtUtc</c>/<c>MarkedPaidByUserId</c>).</summary>
public sealed record MarkInvoicePaidCommand(int InvoiceId, int MarkedByUserId) : IRequest<Result>;
