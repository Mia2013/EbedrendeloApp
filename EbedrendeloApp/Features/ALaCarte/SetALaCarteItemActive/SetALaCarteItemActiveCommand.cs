using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.ALaCarte.SetALaCarteItemActive;

/// <summary>Bidirectional toggle (AC 4.3.2 covers the deactivate direction; reactivating is the same
/// "IsActive" flag going back to true) — used by the one-click toggle icon in AdminALaCarteItems.razor's
/// Műveletek column, not just a one-way kivezetés.</summary>
public sealed record SetALaCarteItemActiveCommand(int Id, bool IsActive) : IRequest<Result>;
