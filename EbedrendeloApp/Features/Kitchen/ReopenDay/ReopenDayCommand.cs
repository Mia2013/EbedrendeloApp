using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Kitchen.ReopenDay;

/// <summary>US-6.2, AC 6.2.3 — feloldja a zárolást; ha a 3 munkanapos határidő még engedi, a nap újra
/// módosíthatóvá válik. A záráskori snapshot változatlanul megmarad (AC 6.3.2) — ez a parancs csak egy
/// <see cref="Domain.Entities.KitchenClosureReopening"/> sort szúr be, a zárást magát nem módosítja.</summary>
public sealed record ReopenDayCommand(DateOnly Date, int ReopenedByUserId) : IRequest<Result>;
