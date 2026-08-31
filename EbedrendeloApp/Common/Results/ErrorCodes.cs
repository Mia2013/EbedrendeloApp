namespace EbedrendeloApp.Common.Results;

public static class ErrorCodes
{
    public const string DeadlinePassed = nameof(DeadlinePassed);
    public const string PeriodClosed = nameof(PeriodClosed);
    public const string DayClosed = nameof(DayClosed);
    public const string DayExcluded = nameof(DayExcluded);
    public const string NotWorkingDay = nameof(NotWorkingDay);
    public const string MenuNotPublished = nameof(MenuNotPublished);
    public const string OutsidePeriod = nameof(OutsidePeriod);
    public const string AlreadyOrdered = nameof(AlreadyOrdered);
    public const string NoActiveOrder = nameof(NoActiveOrder);
    public const string InvalidVariantCode = nameof(InvalidVariantCode);

    public const string Overlaps = nameof(Overlaps);
    public const string NotFutureDate = nameof(NotFutureDate);
    public const string HasOrders = nameof(HasOrders);
    public const string NotFound = nameof(NotFound);
    public const string NoVariants = nameof(NoVariants);
    public const string DuplicateName = nameof(DuplicateName);
}
