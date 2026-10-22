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
}
