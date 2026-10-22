using System.Globalization;

namespace Pinta.SharedKernel.Text;

/// <summary>Translatable catalog text, stored as <c>jsonb</c> <c>{ "pt": "…", "es": "…", "en": "…" }</c>. Portuguese is required and is the fallback.</summary>
public sealed class LocalizedText
{
    public required string Pt { get; set; }

    public string? Es { get; set; }

    public string? En { get; set; }

    /// <summary>The text in the request language (<see cref="CultureInfo.CurrentUICulture"/>), falling back to Portuguese.</summary>
    public string Current => In(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

    public string In(string language) => language switch
    {
        "es" when !string.IsNullOrWhiteSpace(Es) => Es,
        "en" when !string.IsNullOrWhiteSpace(En) => En,
        _ => Pt,
    };
}

/// <summary>API shape of <see cref="LocalizedText"/>; empty translations come back as <c>null</c>.</summary>
public sealed record LocalizedTextDto(string Pt, string? Es, string? En)
{
    public static LocalizedTextDto From(LocalizedText text) => new(text.Pt, text.Es, text.En);

    public LocalizedText ToText() => new() { Pt = Pt.Trim(), Es = Blank(Es), En = Blank(En) };

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
