using System.Globalization;
using System.Resources;

namespace Pinta.Modules.Finance.Resources;

/// <summary>Field validation messages in the request language (pt, es, en).</summary>
internal static class FinanceMessages
{
    private static readonly ResourceManager Resources = new("Pinta.Modules.Finance.Resources.FinanceMessages", typeof(FinanceMessages).Assembly);

    public static string Get(string name, params object[] args)
    {
        var text = Resources.GetString(name, CultureInfo.CurrentUICulture) ?? name;
        return args.Length == 0 ? text : string.Format(CultureInfo.CurrentUICulture, text, args);
    }
}
