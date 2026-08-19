namespace EbedrendeloApp.Domain.Entities;

public sealed class MenuVariant
{
    public int Id { get; set; }
    public required int DailyMenuId { get; set; }
    public DailyMenu? DailyMenu { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}
