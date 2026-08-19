namespace EbedrendeloApp.Domain.Entities;

public sealed class DailyMenu
{
    public int Id { get; set; }
    public required DateOnly Date { get; set; }
    public bool IsPublished { get; set; }
    public string? Note { get; set; }
    public List<MenuVariant> Variants { get; set; } = [];
}
