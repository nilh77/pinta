namespace Pinta.SharedKernel;

/// <summary>Portuguese tax number (NIF): 9 digits with a mod-11 check digit (C4.3).</summary>
public static class TaxId
{
    public static bool IsValid(string? value)
    {
        if (value is null || value.Length != 9 || !value.All(char.IsAsciiDigit))
        {
            return false;
        }

        // First digits in use: 1–3 people, 45 non-residents, 5 companies, 6 public bodies, 7x/8/9x others.
        if (value[0] is '0' || (value[0] is '4' && value[1] is not '5'))
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < 8; i++)
        {
            sum += (value[i] - '0') * (9 - i);
        }

        var remainder = sum % 11;
        var check = remainder < 2 ? 0 : 11 - remainder;
        return value[8] - '0' == check;
    }
}
