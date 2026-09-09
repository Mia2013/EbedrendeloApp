using EbedrendeloApp.Common.Security;
using MediatR;

namespace EbedrendeloApp.Features.Billing.GetBalances;

/// <summary>Nincs önálló user story — AC 5.2.1-et támogató implementációs részlet (admin áttekintés).
/// A 0 Ft-os egyenlegek explicit termékdöntés alapján kimaradnak; ők a kézi jóváírás dialógus
/// autocomplete-jén (GetUsersQuery) keresztül továbbra is elérhetők.</summary>
public sealed record GetBalancesQuery : IRequest<IReadOnlyList<UserBalanceDto>>, IRequireAdmin;

public sealed record UserBalanceDto(int UserId, string DisplayName, string? Igazgatosag, string? Osztaly, int BalanceHuf);
