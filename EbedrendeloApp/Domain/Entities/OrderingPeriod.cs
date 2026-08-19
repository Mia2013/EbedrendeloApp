namespace EbedrendeloApp.Domain.Entities;

public sealed class OrderingPeriod
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required DateOnly StartDate { get; set; }
    public required DateOnly EndDate { get; set; }
    public required DateTime OrderDeadline { get; set; }
    public bool IsOpen { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
}
