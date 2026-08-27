using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Menus.DeleteDailyMenu;

public sealed record DeleteDailyMenuCommand(DateOnly Date, int PerformedByUserId) : IRequest<Result>;
