namespace EbedrendeloApp.Domain.Entities;

public sealed class ALaCarteDailyOffer
{
    public int Id { get; set; }
    public required DateOnly Date { get; set; }
    public required int ALaCarteItemId { get; set; }
    public ALaCarteItem? ALaCarteItem { get; set; }
    public required int Capacity { get; set; }
    public int OrderedCount { get; set; }
}
