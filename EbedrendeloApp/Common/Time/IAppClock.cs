namespace EbedrendeloApp.Common.Time;

public interface IAppClock
{
    DateTimeOffset UtcNow { get; }
    DateTime LocalNow { get; }
    DateOnly Today { get; }
    DateTime ToLocal(DateTimeOffset utc);
}
