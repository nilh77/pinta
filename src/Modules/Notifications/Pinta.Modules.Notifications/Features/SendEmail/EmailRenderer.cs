using System.Globalization;
using System.Resources;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Notifications.Templates;
using Pinta.SharedKernel;

namespace Pinta.Modules.Notifications.Features.SendEmail;

/// <summary>Email texts for one locale, read from Resources/Emails.{locale}.resx (Portuguese is the neutral resource).</summary>
public sealed class EmailText(CultureInfo culture)
{
    private static readonly ResourceManager Resources = new(
        "Pinta.Modules.Notifications.Resources.Emails", typeof(EmailText).Assembly);

    public string this[string key] => Resources.GetString(key, culture) ?? key;

    public string Format(string key, params object[] args) => string.Format(culture, this[key], args);
}

public sealed class EmailRenderer(IServiceProvider services)
{
    public async Task<(string Subject, string Html)> RenderAsync(Email email)
    {
        var text = new EmailText(CultureInfo.GetCultureInfo(email.Locale.ToString().ToLowerInvariant()));
        return email switch
        {
            ConfirmEmailEmail confirm => (
                text["ConfirmEmail.Subject"],
                await RenderAsync<ConfirmEmailTemplate>(new Dictionary<string, object?>
                {
                    [nameof(ConfirmEmailTemplate.Text)] = text,
                    [nameof(ConfirmEmailTemplate.Email)] = confirm,
                })),
            RequestReceivedEmail received => (
                text.Format("RequestReceived.Subject", received.Code),
                await RenderAsync<RequestReceivedTemplate>(new Dictionary<string, object?>
                {
                    [nameof(RequestReceivedTemplate.Text)] = text,
                    [nameof(RequestReceivedTemplate.Email)] = received,
                })),
            _ => throw new NotSupportedException($"No template for {email.GetType().Name}."),
        };
    }

    private async Task<string> RenderAsync<TComponent>(Dictionary<string, object?> parameters)
        where TComponent : IComponent
    {
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<TComponent>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }
}
