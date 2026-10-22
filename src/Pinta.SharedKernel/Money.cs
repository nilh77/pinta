namespace Pinta.SharedKernel;

/// <summary>Euros as <c>decimal</c> in the domain and the database; integer cents in the API (docs/api.md).</summary>
public static class Money
{
    public static long ToCents(decimal euros) => (long)Math.Round(euros * 100, 0, MidpointRounding.AwayFromZero);

    public static decimal FromCents(long cents) => cents / 100m;
}
