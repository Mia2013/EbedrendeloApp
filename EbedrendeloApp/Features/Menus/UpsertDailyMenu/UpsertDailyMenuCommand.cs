using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Menus.UpsertDailyMenu;

public sealed record MenuVariantInput(string Code, string Name, string? Description, int SortOrder);

public sealed record UpsertDailyMenuCommand(
    DateOnly Date,
    string? Note,
    IReadOnlyList<MenuVariantInput> Variants,
    int PerformedByUserId) : IRequest<Result<int>>;
