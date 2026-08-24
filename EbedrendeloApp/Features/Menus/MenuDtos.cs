namespace EbedrendeloApp.Features.Menus;

public sealed record MenuVariantDto(string Code, string Name, string? Description, int SortOrder);

public sealed record DailyMenuDto(DateOnly Date, bool IsPublished, string? Note, IReadOnlyList<MenuVariantDto> Variants);
