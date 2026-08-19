using MediatR;

namespace EbedrendeloApp.Tests.TestSupport;

/// <summary>
/// Minimal hand-rolled <see cref="IMediator"/> test double for bUnit component tests — no mocking
/// library is referenced in this project, and components only ever call the generic
/// <c>Send(IRequest&lt;TResponse&gt;)</c> overload, so this is enough.
/// </summary>
public sealed class FakeMediator : IMediator
{
    private readonly Dictionary<Type, Func<object, Task<object?>>> handlers = [];

    public void Register<TRequest, TResponse>(Func<TRequest, Task<TResponse>> handler)
        where TRequest : IRequest<TResponse>
        => handlers[typeof(TRequest)] = async request => await handler((TRequest)request);

    public void Register<TRequest, TResponse>(Func<TRequest, TResponse> handler)
        where TRequest : IRequest<TResponse>
        => handlers[typeof(TRequest)] = request => Task.FromResult<object?>(handler((TRequest)request));

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (!handlers.TryGetValue(request.GetType(), out var handler))
        {
            throw new InvalidOperationException($"No fake handler registered for {request.GetType().Name}.");
        }

        return InvokeAsync<TResponse>(handler, request);
    }

    public async Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        if (!handlers.TryGetValue(request.GetType(), out var handler))
        {
            throw new InvalidOperationException($"No fake handler registered for {request.GetType().Name}.");
        }

        return await handler(request);
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
        => Send((object)request!, cancellationToken);

    public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
        => Task.CompletedTask;

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    private static async Task<TResponse> InvokeAsync<TResponse>(Func<object, Task<object?>> handler, object request)
        => (TResponse)(await handler(request))!;
}
