using FluentValidation;

namespace EbedrendeloApp.Features.Notifications.MarkNotificationRead;

public sealed class MarkNotificationReadValidator : AbstractValidator<MarkNotificationReadCommand>
{
    public MarkNotificationReadValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.NotificationId).GreaterThan(0);
    }
}
