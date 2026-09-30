using EbedrendeloApp.Domain.Enums;

namespace EbedrendeloApp.Features.Notifications;

/// <summary>Egy értesítés a listához. <paramref name="OnBehalfOfName"/> akkor van kitöltve, ha az
/// értesítés más rendeléséről szól — a leadó kapja, akinek tudnia kell, kinek a nevében rendelt (AC 8.1.3).</summary>
public sealed record NotificationDto(
    int Id,
    NotificationType Type,
    string Title,
    string Message,
    DateOnly? RelatedDate,
    DateTime CreatedAtLocal,
    bool IsRead,
    string? OnBehalfOfName);

public sealed record NotificationCountsDto(int Total, int Unread);
