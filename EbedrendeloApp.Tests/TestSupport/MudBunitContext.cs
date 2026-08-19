using Bunit;

namespace EbedrendeloApp.Tests.TestSupport;

/// <summary>
/// bUnit's synchronous <see cref="BunitContext.Dispose()"/> throws when the DI container holds
/// MudBlazor 9.x services that only implement <see cref="IAsyncDisposable"/> (KeyInterceptorService,
/// PointerEventsNoneService, PopoverService, ...) — xUnit v2 only calls the sync <c>Dispose</c> hook,
/// never <c>DisposeAsync</c>. Disposing the service provider asynchronously first, then letting the
/// base class's own (now-redundant) synchronous disposal run against an already-disposed provider,
/// works around it.
/// </summary>
public abstract class MudBunitContext : BunitContext
{
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
            return;
        }

        base.Dispose(disposing);
    }
}
