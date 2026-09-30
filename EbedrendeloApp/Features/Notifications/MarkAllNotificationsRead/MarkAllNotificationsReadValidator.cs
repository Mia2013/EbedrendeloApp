using FluentValidation;

namespace EbedrendeloApp.Features.Notifications.MarkAllNotificationsRead;

public sealed class MarkAllNotificationsReadValidator : AbstractValidator<MarkAllNotificationsReadCommand>
{
    public MarkAllNotificationsReadValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
    }
}
