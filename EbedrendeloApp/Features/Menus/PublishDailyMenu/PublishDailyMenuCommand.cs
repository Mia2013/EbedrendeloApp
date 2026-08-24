using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Menus.PublishDailyMenu;

public sealed record PublishDailyMenuCommand(DateOnly Date) : IRequest<Result>;
