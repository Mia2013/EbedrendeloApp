using Bunit;
using EbedrendeloApp.Components.Layout;
using EbedrendeloApp.Tests.TestSupport;

namespace EbedrendeloApp.Tests.Components.Layout;

/// <summary>
/// A SignalR-szakadáskor felugró dialógus. A Blazor saját JS-e id/osztálynév alapján kapcsolgatja a
/// benne lévő állapotokat, ezért ezek a nevek nem átnevezhetők — és a szövegnek magyarnak kell lennie,
/// mert ez az egyetlen, amit a dolgozó a kapcsolat elvesztésekor lát.
/// </summary>
public class ReconnectModalTests : MudBunitContext
{
    public ReconnectModalTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    [Fact]
    public void Keeps_the_ids_and_class_names_the_blazor_runtime_targets()
    {
        var cut = Render<ReconnectModal>();

        Assert.NotNull(cut.Find("#components-reconnect-modal"));
        Assert.NotNull(cut.Find("#components-reconnect-button"));
        Assert.NotNull(cut.Find("#components-resume-button"));
        Assert.NotNull(cut.Find("#components-seconds-to-next-attempt"));
        Assert.NotNull(cut.Find(".components-reconnect-first-attempt-visible"));
        Assert.NotNull(cut.Find(".components-reconnect-failed-visible"));
        Assert.NotNull(cut.Find(".components-pause-visible"));
        Assert.NotNull(cut.Find(".components-resume-failed-visible"));
    }

    [Fact]
    public void Speaks_hungarian_in_every_state()
    {
        var cut = Render<ReconnectModal>();

        Assert.Contains("Újracsatlakozás a szerverhez", cut.Markup);
        Assert.Contains("Az újracsatlakozás nem sikerült", cut.Markup);
        Assert.Contains("Újrapróbálkozás", cut.Markup);
        Assert.Contains("A szerver felfüggesztette a munkamenetet", cut.Markup);
        Assert.Contains("Folytatás", cut.Markup);

        Assert.DoesNotContain("Rejoining", cut.Markup);
        Assert.DoesNotContain("Retry", cut.Markup);
        Assert.DoesNotContain("Resume", cut.Markup);
    }
}
