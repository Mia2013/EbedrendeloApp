using EbedrendeloApp.Common.Calendar;
using EbedrendeloApp.Domain.Entities;

namespace EbedrendeloApp.Tests.Common.Calendar;

public class WorkingDayCalculatorTests
{
    private readonly WorkingDayCalculator sut = new();

    private static readonly AppSetting DefaultSettings = new()
    {
        MenuPortionHuf = 1400,
        ChangeDeadlineWorkingDays = 3,
        ChangeDeadlineLocalTime = new TimeOnly(11, 0),
        ALaCarteOrderDeadlineLocalTime = new TimeOnly(10, 30),
    };

    private static readonly IReadOnlySet<DateOnly> NoExclusions = new HashSet<DateOnly>();

    [Theory]
    [InlineData(2026, 8, 17, true)]  // Monday
    [InlineData(2026, 8, 22, false)] // Saturday
    [InlineData(2026, 8, 23, false)] // Sunday
    public void IsWorkingDay_treats_weekends_as_non_working(int year, int month, int day, bool expected)
    {
        var date = new DateOnly(year, month, day);
        Assert.Equal(expected, sut.IsWorkingDay(date, NoExclusions));
    }

    [Fact]
    public void IsWorkingDay_treats_excluded_weekday_as_non_working()
    {
        var date = new DateOnly(2026, 8, 19); // Wednesday
        var excluded = new HashSet<DateOnly> { date };

        Assert.False(sut.IsWorkingDay(date, excluded));
    }

    [Fact]
    public void ChangeDeadline_counts_back_three_working_days_from_thursday_to_monday()
    {
        // Thursday 2026-08-20 -> Wed(1), Tue(2), Mon(3) -> Monday 2026-08-17 11:00
        var serviceDate = new DateOnly(2026, 8, 20);

        var deadline = sut.ChangeDeadline(serviceDate, DefaultSettings, NoExclusions);

        Assert.Equal(new DateTime(2026, 8, 17, 11, 0, 0), deadline);
    }

    [Fact]
    public void ChangeDeadline_skips_weekend()
    {
        // Monday 2026-08-24 -> Fri(1), Thu(2), Wed(3) -> Wednesday 2026-08-19 11:00
        var serviceDate = new DateOnly(2026, 8, 24);

        var deadline = sut.ChangeDeadline(serviceDate, DefaultSettings, NoExclusions);

        Assert.Equal(new DateTime(2026, 8, 19, 11, 0, 0), deadline);
    }

    [Fact]
    public void ChangeDeadline_skips_excluded_days()
    {
        // Thursday 2026-08-20, with Wednesday 2026-08-19 excluded:
        // Wed(excluded, skipped), Tue(1), Mon(2), Fri-prev-week(3) -> Friday 2026-08-14 11:00
        var serviceDate = new DateOnly(2026, 8, 20);
        var excluded = new HashSet<DateOnly> { new DateOnly(2026, 8, 19) };

        var deadline = sut.ChangeDeadline(serviceDate, DefaultSettings, excluded);

        Assert.Equal(new DateTime(2026, 8, 14, 11, 0, 0), deadline);
    }

    [Fact]
    public void ChangeDeadline_crosses_month_boundary()
    {
        // Tuesday 2026-09-01 -> Mon(1) 08-31, Fri(2) 08-28, Thu(3) 08-27 -> 2026-08-27 11:00
        var serviceDate = new DateOnly(2026, 9, 1);

        var deadline = sut.ChangeDeadline(serviceDate, DefaultSettings, NoExclusions);

        Assert.Equal(new DateTime(2026, 8, 27, 11, 0, 0), deadline);
    }

    [Fact]
    public void ChangeDeadline_always_lands_on_a_working_day()
    {
        for (var i = 0; i < 60; i++)
        {
            var serviceDate = new DateOnly(2026, 1, 1).AddDays(i);
            var deadline = sut.ChangeDeadline(serviceDate, DefaultSettings, NoExclusions);

            Assert.True(sut.IsWorkingDay(DateOnly.FromDateTime(deadline), NoExclusions));
        }
    }

    [Fact]
    public void CanChange_is_true_just_before_the_deadline()
    {
        var serviceDate = new DateOnly(2026, 8, 20); // Thursday, deadline Monday 2026-08-17 11:00
        var now = new DateTime(2026, 8, 17, 10, 59, 0);

        Assert.True(sut.CanChange(serviceDate, now, DefaultSettings, NoExclusions, hasKitchenClosure: false));
    }

    [Fact]
    public void CanChange_is_false_just_after_the_deadline()
    {
        var serviceDate = new DateOnly(2026, 8, 20); // Thursday, deadline Monday 2026-08-17 11:00
        var now = new DateTime(2026, 8, 17, 11, 1, 0);

        Assert.False(sut.CanChange(serviceDate, now, DefaultSettings, NoExclusions, hasKitchenClosure: false));
    }

    [Fact]
    public void CanChange_is_false_when_the_day_is_closed_even_before_the_deadline()
    {
        var serviceDate = new DateOnly(2026, 8, 20);
        var now = new DateTime(2026, 8, 17, 9, 0, 0);

        Assert.False(sut.CanChange(serviceDate, now, DefaultSettings, NoExclusions, hasKitchenClosure: true));
    }
}
