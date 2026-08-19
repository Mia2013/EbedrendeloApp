using EbedrendeloApp.Domain.Enums;

namespace EbedrendeloApp.Domain.Entities;

public sealed class ALaCarteItem
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required ALaCarteCategory Category { get; set; }
    public required int PriceHuf { get; set; }
    public bool IsActive { get; set; } = true;
}
