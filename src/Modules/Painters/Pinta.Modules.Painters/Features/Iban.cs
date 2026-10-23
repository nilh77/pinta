using System.Numerics;
using Microsoft.AspNetCore.DataProtection;

namespace Pinta.Modules.Painters.Features;

/// <summary>Portuguese IBAN: <c>PT50</c> and 21 digits, checked with ISO 13616 mod-97 (docs/screens.md, alta).</summary>
public static class Iban
{
    public static string Normalize(string value) => new string([.. value.Where(c => !char.IsWhiteSpace(c))]).ToUpperInvariant();

    public static bool IsValidPortuguese(string? value)
    {
        if (value is null)
        {
            return false;
        }

        var iban = Normalize(value);
        if (iban.Length != 25 || !iban.StartsWith("PT") || !iban[2..].All(char.IsAsciiDigit))
        {
            return false;
        }

        // Move the country and check digits to the end, letters to numbers (P=25, T=29), and take mod 97.
        var rearranged = iban[4..] + "2529" + iban[2..4];
        return BigInteger.Parse(rearranged) % 97 == 1;
    }

    /// <summary>Only the country and the last 4 digits are ever shown: <c>PT50 •••• 1234</c>.</summary>
    public static string Mask(string last4) => $"PT50 •••• {last4}";
}

/// <summary>Encrypts the IBAN at rest with ASP.NET Core Data Protection (CLAUDE.md, sensitive data).</summary>
public sealed class IbanProtector(IDataProtectionProvider provider)
{
    private readonly IDataProtector _protector = provider.CreateProtector("Pinta.Painters.Iban");

    public string Protect(string iban) => _protector.Protect(Iban.Normalize(iban));

    public string Unprotect(string encrypted) => _protector.Unprotect(encrypted);
}
