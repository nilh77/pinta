namespace Pinta.SharedKernel.Text;

public static class Csv
{
    /// <summary>One CSV field: quoted when needed, and a leading = + - @ neutralised so spreadsheets never run it.</summary>
    public static string Field(string value)
    {
        if (value.Length > 0 && "=+-@".Contains(value[0]))
        {
            value = "'" + value;
        }

        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
