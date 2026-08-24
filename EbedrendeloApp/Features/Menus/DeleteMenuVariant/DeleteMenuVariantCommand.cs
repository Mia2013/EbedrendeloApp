using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Menus.DeleteMenuVariant;

public sealed record DeleteMenuVariantCommand(DateOnly Date, string VariantCode, int PerformedByUserId) : IRequest<Result>;
