using EbedrendeloApp.Domain.Enums;
using MediatR;

namespace EbedrendeloApp.Features.ALaCarte.GetALaCarteMonthlySummary;

public sealed record GetALaCarteMonthlySummaryQuery(int Year, int Month) : IRequest<ALaCarteMonthlySummaryDto>;

/// <summary>Napi bontású sor — a havi konyhai lista dátum × tétel mátrixot épít belőle
/// (lásd AdminALaCarteKitchenSummary.razor), ezért itt (a napi összesítővel, <see cref="EbedrendeloApp.Features.ALaCarte.GetALaCarteDailySummary.ALaCarteSummaryLineDto"/>-vel
/// ellentétben) a dátum is a sor része.</summary>
public sealed record ALaCarteMonthlyLineDto(DateOnly Date, ALaCarteCategory Category, string ItemName, int Count);

/// <summary>A mátrix oszlopainak definíciója — egy tétel akkor kap oszlopot, ha a hónap valamelyik
/// napján ténylegesen rendelhető (Capacity &gt; 0) volt, függetlenül attól, hogy lett-e belőle rendelés.
/// Egy tétel, ami a hónapban egyszer sem volt így ajánlva, nem kap oszlopot.</summary>
public sealed record ALaCarteMonthlyOfferedItemDto(ALaCarteCategory Category, string ItemName);

/// <summary>A <paramref name="SoupPortionCount"/> ugyanúgy levezetett érték, mint a napi
/// <see cref="EbedrendeloApp.Features.ALaCarte.GetALaCarteDailySummary.ALaCarteDailySummaryDto"/>-n (AC 4.6.3) — a hónap összes Főétel-sorának darabszáma.</summary>
public sealed record ALaCarteMonthlySummaryDto(
    int Year,
    int Month,
    int SoupPortionCount,
    IReadOnlyList<ALaCarteMonthlyLineDto> Lines,
    IReadOnlyList<ALaCarteMonthlyOfferedItemDto> OfferedItems);
