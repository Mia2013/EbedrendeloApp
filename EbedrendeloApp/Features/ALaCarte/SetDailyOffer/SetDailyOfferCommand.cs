using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.ALaCarte.SetDailyOffer;

/// <summary>Upsert: ha a napra már van ajánlat erre a tételre, a <paramref name="Capacity"/>-t
/// frissíti; egyébként létrehozza. Leves kategóriánál a <paramref name="Capacity"/> figyelmen kívül
/// marad (mindig korlátlan).</summary>
public sealed record SetDailyOfferCommand(DateOnly Date, int ALaCarteItemId, int Capacity) : IRequest<Result<ALaCarteDailyOfferDto>>;
