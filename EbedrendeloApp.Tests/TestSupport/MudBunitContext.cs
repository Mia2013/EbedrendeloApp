using Bunit;
using EbedrendeloApp.Common.Time;
using Microsoft.Extensions.DependencyInjection;

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
    protected MudBunitContext()
    {
        // A naptár-komponensek a szerverrel közös órából veszik a „ma"-t, nem a gép helyi idejéből
        // (l. WeekGrid.EffectiveToday). A tesztek a valós mai napot várják, ezért itt az az alapérték;
        // aki rögzített dátumot akar, felülírja egy saját FixedAppClock-kal.
        // SpecifyKind kell: a FixedAppClock DateTimeOffset(…, TimeSpan.Zero)-t képez, ami Local Kind-ra
        // ArgumentException-t dob — a DateTime.Today pedig Local.
        Services.AddSingleton<IAppClock>(
            new FixedAppClock(DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Unspecified)));
    }

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
