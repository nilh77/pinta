using System.Globalization;
using System.Resources;

namespace Pinta.Modules.Catalog.Resources;

/// <summary>Field validation messages in the request language (pt, es, en).</summary>
internal static class CatalogMessages
{
    private static readonly ResourceManager Resources = new("Pinta.Modules.Catalog.Resources.CatalogMessages", typeof(CatalogMessages).Assembly);

    public static string Get(string name, params object[] args)
    {
        var text = Resources.GetString(name, CultureInfo.CurrentUICulture) ?? name;
        return args.Length == 0 ? text : string.Format(CultureInfo.CurrentUICulture, text, args);
    }
}
