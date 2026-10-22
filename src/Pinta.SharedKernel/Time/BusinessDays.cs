namespace Pinta.SharedKernel.Time;

/// <summary>
/// Working days are Monday to Friday in Europe/Lisbon; the MVP ignores public holidays (docs/screens.md, screen 6).
/// </summary>
public static class BusinessDays
{
    public static readonly TimeZoneInfo Lisbon = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

    /// <summary>Today's date in Lisbon.</summary>
    public static DateOnly Today(TimeProvider clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Lisbon).DateTime);

    public static bool IsBusinessDay(DateOnly date) => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    /// <summary>The date <paramref name="days"/> working days after <paramref name="from"/> (not counting it).</summary>
    public static DateOnly Add(DateOnly from, int days)
    {
        var date = from;
        while (days > 0)
        {
            date = date.AddDays(1);
            if (IsBusinessDay(date))
            {
                days--;
            }
        }

        return date;
    }
}
