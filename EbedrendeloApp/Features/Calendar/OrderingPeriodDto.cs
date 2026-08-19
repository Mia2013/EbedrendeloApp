namespace EbedrendeloApp.Features.Calendar;

public sealed record OrderingPeriodDto(
    int Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTime OrderDeadline,
    bool IsOpen,
    bool HasOrders);
