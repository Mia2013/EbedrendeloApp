namespace EbedrendeloApp.Domain.Entities;

public sealed class AppSetting
{
    public int Id { get; set; }
    public required int MenuPortionHuf { get; set; }
    public required int ChangeDeadlineWorkingDays { get; set; }
    public required TimeOnly ChangeDeadlineLocalTime { get; set; }
    public required TimeOnly ALaCarteOrderDeadlineLocalTime { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public int? UpdatedByUserId { get; set; }
}
