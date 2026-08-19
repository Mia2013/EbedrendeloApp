using EbedrendeloApp.Domain.Enums;

namespace EbedrendeloApp.Domain.Entities;

public sealed class UserNotification
{
    public int Id { get; set; }
    public required int UserId { get; set; }
    public required NotificationType Type { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
    public DateOnly? RelatedDate { get; set; }
    public int? RelatedMenuOrderId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}
