using EbedrendeloApp.Common.Time;

namespace EbedrendeloApp.Tests.TestSupport;

public sealed class FixedAppClock(DateTime localNow) : IAppClock
{
    public DateTimeOffset UtcNow { get; } = new DateTimeOffset(localNow, TimeSpan.Zero);

    public DateTime LocalNow { get; } = localNow;

    public DateOnly Today { get; } = DateOnly.FromDateTime(localNow);

    public DateTime ToLocal(DateTimeOffset utc) => utc.DateTime;
}
