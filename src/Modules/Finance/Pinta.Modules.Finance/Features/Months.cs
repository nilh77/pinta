using System.Globalization;
using Pinta.SharedKernel.Time;

namespace Pinta.Modules.Finance.Features;

/// <summary>Months of the finance screens (<c>YYYY-MM</c>), bounded by midnight in Lisbon.</summary>
internal static class Months
{
    public static DateOnly Current(TimeProvider clock)
    {
        var today = BusinessDays.Today(clock);
        return new DateOnly(today.Year, today.Month, 1);
    }

    /// <summary>The first day of <paramref name="month"/>, the current month when null; false when it is not <c>YYYY-MM</c>.</summary>
    public static bool TryParse(string? month, TimeProvider clock, out DateOnly first)
    {
        if (month is null)
        {
            first = Current(clock);
            return true;
        }

        return DateOnly.TryParseExact($"{month}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out first);
    }

    public static DateOnly Of(DateOnly day) => new(day.Year, day.Month, 1);

    public static string Format(DateOnly first) => first.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>[start of the month, start of the next) as UTC instants.</summary>
    public static (DateTimeOffset From, DateTimeOffset To) Bounds(DateOnly first) => (LisbonStart(first), LisbonStart(first.AddMonths(1)));

    private static DateTimeOffset LisbonStart(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, BusinessDays.Lisbon.GetUtcOffset(local)).ToUniversalTime();
    }
}
