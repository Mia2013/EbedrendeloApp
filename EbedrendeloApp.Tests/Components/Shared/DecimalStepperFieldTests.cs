using Bunit;
using EbedrendeloApp.Components.Shared;
using EbedrendeloApp.Tests.TestSupport;
using Microsoft.AspNetCore.Components;
using MudBlazor.Services;

namespace EbedrendeloApp.Tests.Components.Shared;

public class DecimalStepperFieldTests : MudBunitContext
{
    public DecimalStepperFieldTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Formats_the_value_with_a_comma_decimal_separator()
    {
        var cut = Render<DecimalStepperField>(p => p
            .Add(x => x.Label, "Zsír")
            .Add(x => x.Value, 1.8m));

        var input = cut.Find("input");
        Assert.Equal("1,8", input.GetAttribute("value"));
    }

    [Fact]
    public void Typing_a_comma_decimal_value_raises_ValueChanged_with_the_parsed_decimal()
    {
        decimal? received = null;
        var cut = Render<DecimalStepperField>(p => p
            .Add(x => x.Label, "Só")
            .Add(x => x.Value, (decimal?)null)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<decimal?>(this, v => received = v)));

        var input = cut.Find("input");
        input.Change("2,5");

        Assert.Equal(2.5m, received);
    }

    [Fact]
    public void Typing_a_dot_decimal_value_is_also_accepted()
    {
        decimal? received = null;
        var cut = Render<DecimalStepperField>(p => p
            .Add(x => x.Label, "Só")
            .Add(x => x.Value, (decimal?)null)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<decimal?>(this, v => received = v)));

        var input = cut.Find("input");
        input.Change("2.5");

        Assert.Equal(2.5m, received);
    }

    [Fact]
    public async Task Incrementing_adds_the_step()
    {
        decimal? received = null;
        var cut = Render<DecimalStepperField>(p => p
            .Add(x => x.Label, "Cukor")
            .Add(x => x.Value, 1.0m)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<decimal?>(this, v => received = v)));

        var incrementButton = cut.FindAll("button")[0];
        await cut.InvokeAsync(() => incrementButton.Click());

        Assert.Equal(1.1m, received);
    }

    [Fact]
    public async Task Decrementing_below_the_minimum_clamps_to_the_minimum()
    {
        decimal? received = null;
        var cut = Render<DecimalStepperField>(p => p
            .Add(x => x.Label, "Fehérje")
            .Add(x => x.Value, 0m)
            .Add(x => x.Min, 0m)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<decimal?>(this, v => received = v)));

        var decrementButton = cut.FindAll("button")[1];
        await cut.InvokeAsync(() => decrementButton.Click());

        Assert.Equal(0m, received);
    }
}
