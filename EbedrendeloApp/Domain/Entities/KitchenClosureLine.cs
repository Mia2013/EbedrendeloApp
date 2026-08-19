namespace EbedrendeloApp.Domain.Entities;

public sealed class KitchenClosureLine
{
    public int Id { get; set; }
    public required int KitchenClosureId { get; set; }
    public KitchenClosure? KitchenClosure { get; set; }
    public required string VariantCode { get; set; }
    public required string VariantNameSnapshot { get; set; }
    public required int Quantity { get; set; }
}
