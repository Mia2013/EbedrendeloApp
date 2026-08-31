namespace EbedrendeloApp.Domain.Entities;

public sealed class MenuVariant
{
    public int Id { get; set; }
    public required int DailyMenuId { get; set; }
    public DailyMenu? DailyMenu { get; set; }
    public required string Code { get; set; }

    /// <summary>Denormalized copy of <see cref="SoupDish"/>.Name, kept in sync by UpsertDailyMenuHandler
    /// from the loaded catalog row — never trusted from client input, so it can't drift from the FK.</summary>
    public required string SoupName { get; set; }
    public string? MainCourseName { get; set; }

    public required int SoupDishId { get; set; }
    public MenuDish? SoupDish { get; set; }
    public int? MainCourseDishId { get; set; }
    public MenuDish? MainCourseDish { get; set; }

    public int SortOrder { get; set; }

    /// <summary>Soft-delete marker — see <see cref="DailyMenu.RemovedAtUtc"/> for why this isn't a hard delete.</summary>
    public DateTime? RemovedAtUtc { get; set; }
}
