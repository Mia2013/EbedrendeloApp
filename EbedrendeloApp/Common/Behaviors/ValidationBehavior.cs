using FluentValidation;
using MediatR;

namespace EbedrendeloApp.Common.Behaviors;

/// <summary>
/// Runs FluentValidation validators before the handler and throws on failure (NFR-2 in
/// 01-szerver-architektura.md) — expected business outcomes are <c>Result</c>/<c>Result&lt;T&gt;</c>,
/// a validation failure is a genuine input-shape bug.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);
        var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        return await next(cancellationToken);
    }
}
