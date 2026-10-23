using Pinta.SharedKernel.Time;

namespace Pinta.SharedKernel.Tests;

public class BusinessDaysTests
{
    [Theory]
    [InlineData("2026-10-22", 3, "2026-10-27")] // Thursday + 3 → Tuesday
    [InlineData("2026-10-24", 1, "2026-10-26")] // Saturday + 1 → Monday
    [InlineData("2026-10-23", 2, "2026-10-27")] // Friday + 2 → Tuesday
    [InlineData("2026-10-22", 0, "2026-10-22")]
    public void C4_2_adds_working_days_skipping_weekends(string from, int days, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), BusinessDays.Add(DateOnly.Parse(from), days));

    [Fact]
    public void Today_is_the_date_in_Lisbon()
    {
        // 23:30 UTC on 31 May is already 1 June in Lisbon (summer time, UTC+1).
        var clock = new FixedClock(new DateTimeOffset(2026, 5, 31, 23, 30, 0, TimeSpan.Zero));
        Assert.Equal(new DateOnly(2026, 6, 1), BusinessDays.Today(clock));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Theory]
    // Thursday 22 Oct 2026, 10:00 Lisbon (WEST, UTC+1); due Monday 26: Thu 14 h + Fri 24 h + Mon 24 h, weekend skipped.
    [InlineData("2026-10-22T09:00:00Z", "2026-10-26", 62)]
    // Friday 23 at 18:00, due that day: 6 h left (amber).
    [InlineData("2026-10-23T17:00:00Z", "2026-10-23", 6)]
    // Saturday, due Monday: only Monday counts.
    [InlineData("2026-10-24T09:00:00Z", "2026-10-26", 24)]
    // The day after the due date: overdue (red).
    [InlineData("2026-10-27T09:00:00Z", "2026-10-26", -1)]
    public void A2_1_working_hours_left_until_the_end_of_the_due_date(string now, string dueOn, double expected) =>
        Assert.Equal(expected, BusinessDays.WorkingHoursLeft(DateTimeOffset.Parse(now), DateOnly.Parse(dueOn)), precision: 6);
}
