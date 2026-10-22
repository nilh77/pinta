namespace Pinta.Pricing.Tests;

/// <summary>Refunds on a 300,00 € deposit (docs/pricing-engine.md).</summary>
public class CancellationPolicyTests
{
    [Theory]
    [InlineData(null, "300.00")]
    [InlineData(7, "300.00")]
    [InlineData(6, "150.00")]
    [InlineData(0, "150.00")]
    public void Refund_follows_the_seven_day_rule(int? daysBefore, string expected) =>
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            CancellationPolicy.Refund(SeedSnapshot.Value.Rules, 300.00m, daysBefore));
}
