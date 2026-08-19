namespace EbedrendeloApp.Domain.Entities;

public sealed class ExcludedDay
{
    public int Id { get; set; }
    public required DateOnly Date { get; set; }
    public required string Reason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public required int CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
}
