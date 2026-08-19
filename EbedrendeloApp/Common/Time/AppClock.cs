namespace EbedrendeloApp.Common.Time;

public sealed class AppClock(TimeProvider timeProvider) : IAppClock
{
    private static readonly TimeZoneInfo BudapestTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Budapest");

    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    public DateTime LocalNow => ToLocal(UtcNow);

    public DateOnly Today => DateOnly.FromDateTime(LocalNow);

    public DateTime ToLocal(DateTimeOffset utc) => TimeZoneInfo.ConvertTime(utc, BudapestTimeZone).DateTime;
}
