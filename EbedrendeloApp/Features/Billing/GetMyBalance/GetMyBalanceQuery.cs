using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Billing.GetMyBalance;

/// <summary>AC 5.1.1 — the balance is Σ CreditEntry.RemainingHuf, usable immediately (no EligibleFrom).
/// Menu-order-only scope (AC 5.1.2) is a presentation concern, not a data one.</summary>
public sealed record GetMyBalanceQuery(int UserId) : IRequest<Result<int>>;
