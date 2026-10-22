using System.Globalization;
using System.Resources;

namespace Pinta.Modules.Quoting.Resources;

/// <summary>Field validation messages in the request language (pt, es, en), keyed by <see cref="Pricing.QuoteErrorCodes"/>.</summary>
internal static class QuotingMessages
{
    private static readonly ResourceManager Resources = new("Pinta.Modules.Quoting.Resources.QuotingMessages", typeof(QuotingMessages).Assembly);

    public static string Get(string name) => Resources.GetString(name, CultureInfo.CurrentUICulture) ?? name;
}
