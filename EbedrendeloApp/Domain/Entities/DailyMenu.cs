namespace EbedrendeloApp.Domain.Entities;

public sealed class DailyMenu
{
    public int Id { get; set; }
    public required DateOnly Date { get; set; }
    public bool IsPublished { get; set; }
    public string? Note { get; set; }

    /// <summary>
    /// Soft-delete marker (DeleteDailyMenuCommand). Never hard-deleted: MenuOrder.MenuVariantId is a
    /// Restrict FK, so any day that ever had an order (even later cancelled) can't have its variants
    /// physically removed without breaking that order's audit trail. A later UpsertDailyMenuCommand for
    /// the same Date revives the row (the unique index on Date requires it, since deleted rows still
    /// occupy that Date).
    /// </summary>
    public DateTime? RemovedAtUtc { get; set; }

    public List<MenuVariant> Variants { get; set; } = [];
}
