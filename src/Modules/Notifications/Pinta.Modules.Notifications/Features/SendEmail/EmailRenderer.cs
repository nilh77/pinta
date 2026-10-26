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
    public CultureInfo Culture => culture;

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
            PriceConfirmedEmail e => Notice(text, e, text.Format("PriceConfirmed.Subject", e.Code), greeting: e.Name,
                paragraphs: [text["PriceConfirmed.Body"]],
                rows: [(text["Common.Code"], e.Code), (text["PriceConfirmed.Total"], Euros(text, e.TotalCents)), (text["PriceConfirmed.Deposit"], Euros(text, e.DepositCents)), (text["PriceConfirmed.ValidUntil"], Date(text, e.ValidUntil))],
                quote: e.Note, button: (text["Common.SeeJob"], e.JobUrl)),
            RequestRejectedEmail e => Notice(text, e, text.Format("RequestRejected.Subject", e.Code), greeting: e.Name,
                paragraphs: [text["RequestRejected.Body"]], quote: e.Reason),
            PriceExpiredEmail e => Notice(text, e, text.Format("PriceExpired.Subject", e.Code), greeting: e.Name,
                paragraphs: [text["PriceExpired.Body"]], button: (text["Common.SeeJob"], e.JobUrl)),
            SiteVisitScheduledEmail e => Notice(text, e, text.Format("SiteVisitScheduled.Subject", e.Code), greeting: e.Name,
                paragraphs: [text["SiteVisitScheduled.Body"]],
                rows: [(text["Common.Code"], e.Code), (text["SiteVisitScheduled.When"], DateTime(text, e.VisitAt)), (text["SiteVisitScheduled.Who"], e.VisitorName), (text["SiteVisitScheduled.Price"], Euros(text, e.PriceCents))]),
            ClientQuestionEmail e => Notice(text, e, text.Format("ClientQuestion.Subject", e.Code),
                paragraphs: [text.Format("ClientQuestion.Body", e.ClientName, e.ClientEmail)], quote: e.Message, button: (text["Common.OpenAdmin"], e.AdminUrl)),
            ClientRejectedPriceEmail e => Notice(text, e, text.Format("ClientRejectedPrice.Subject", e.Code),
                paragraphs: [text.Format("ClientRejectedPrice.Body", e.ClientName)], quote: e.Reason ?? text["ClientRejectedPrice.NoReason"], button: (text["Common.OpenAdmin"], e.AdminUrl)),
            ResetPasswordEmail e => Notice(text, e, text["ResetPassword.Subject"], greeting: e.Name,
                paragraphs: [text["ResetPassword.Body"], text["ResetPassword.Ignore"]], button: (text["ResetPassword.Button"], e.ResetUrl)),
            PaymentReceivedEmail e => Notice(text, e, text.Format("PaymentReceived.Subject", e.Code), greeting: e.Name,
                paragraphs: e.MbWay ? [text["PaymentReceived.Body"], text["PaymentReceived.MbWay"]] : [text["PaymentReceived.Body"]],
                rows: [(text["Common.Code"], e.Code), (text[$"Payment.Kind.{e.Kind}"], Euros(text, e.AmountCents))],
                button: (text["Common.SeeJob"], e.JobUrl)),
            MultibancoReferenceEmail e => Notice(text, e, text.Format("Multibanco.Subject", e.Code), greeting: e.Name,
                paragraphs: [text["Multibanco.Body"]],
                rows: [(text["Multibanco.Entity"], e.Entity), (text["Multibanco.Reference"], e.Reference), (text["Payment.Amount"], Euros(text, e.AmountCents)),
                    .. e.ExpiresAt is { } deadline ? new[] { (text["Multibanco.Deadline"], DateTime(text, deadline)) } : []],
                button: (text["Common.SeeJob"], e.JobUrl)),
            RefundEmail e => Notice(text, e, text.Format("Refund.Subject", e.Code), greeting: e.Name,
                paragraphs: [text["Refund.Body"]], rows: [(text["Common.Code"], e.Code), (text["Payment.Amount"], Euros(text, e.AmountCents))],
                button: (text["Common.SeeJob"], e.JobUrl)),
            AutoRefundEmail e => Notice(text, e, text.Format("AutoRefund.Subject", e.Code),
                paragraphs: [text["AutoRefund.Body"]], rows: [(text[$"Payment.Kind.{e.Kind}"], Euros(text, e.AmountCents))],
                button: (text["Common.OpenAdmin"], e.AdminUrl)),
            PaymentDisputeEmail e => Notice(text, e, text.Format("Dispute.Subject", e.Code),
                paragraphs: [text["Dispute.Body"]], rows: [(text["Payment.Amount"], Euros(text, e.AmountCents))],
                button: e.AdminUrl is null ? null : (text["Common.OpenAdmin"], e.AdminUrl)),
            PainterInvitationEmail e => Notice(text, e, text["PainterInvitation.Subject"], greeting: e.Name,
                paragraphs: [text["PainterInvitation.Body"], text["PainterInvitation.Validity"]], button: (text["PainterInvitation.Button"], e.SignUpUrl)),
            AssignmentOfferedEmail e => Notice(text, e, text.Format("AssignmentOffered.Subject", e.Code), greeting: e.Name,
                paragraphs: [text["AssignmentOffered.Body"]],
                rows: [(text["Common.Code"], e.Code), (text["AssignmentOffered.Where"], e.Municipality), (text["AssignmentOffered.When"], Date(text, e.ScheduledOn)),
                    (text["AssignmentOffered.Pay"], Euros(text, e.PainterPayCents))],
                button: (text["AssignmentOffered.Button"], e.Url)),
            AssignmentDeclinedEmail e => Notice(text, e, text.Format("AssignmentDeclined.Subject", e.Code),
                paragraphs: [text.Format("AssignmentDeclined.Body", e.PainterName)], quote: e.Reason, button: (text["Common.OpenAdmin"], e.AdminUrl)),
            AssignmentUnansweredEmail e => Notice(text, e, text.Format("AssignmentUnanswered.Subject", e.Code),
                paragraphs: [text.Format("AssignmentUnanswered.Body", e.PainterName)], button: (text["Common.OpenAdmin"], e.AdminUrl)),
            JobScheduledEmail e => Notice(text, e, text.Format("JobScheduled.Subject", e.Code), greeting: e.Name,
                paragraphs: [text["JobScheduled.Body"]],
                rows: [(text["Common.Code"], e.Code), (text["JobScheduled.Start"], Date(text, e.ScheduledOn)), (text["JobScheduled.Painter"], e.PainterName)],
                button: (text["Common.SeeJob"], e.JobUrl)),
            JobFinishedEmail e => Notice(text, e, text.Format("JobFinished.Subject", e.Code), greeting: e.Name,
                paragraphs: [text["JobFinished.Body"]],
                rows: [(text["Common.Code"], e.Code), (text["JobFinished.Balance"], Euros(text, e.BalanceCents))],
                button: (text["JobFinished.Button"], e.JobUrl)),
            SiteVisitAssignedEmail e => Notice(text, e, text.Format("SiteVisitAssigned.Subject", e.Code), greeting: e.Name,
                paragraphs: [text["SiteVisitAssigned.Body"]],
                rows: [(text["Common.Code"], e.Code), (text["SiteVisitAssigned.When"], DateTime(text, e.VisitAt)), (text["SiteVisitAssigned.Where"], e.Municipality)],
                button: (text["SiteVisitAssigned.Button"], e.Url)),
            SiteVisitReportedEmail e => Notice(text, e, text.Format("SiteVisitReported.Subject", e.Code),
                paragraphs: [text.Format("SiteVisitReported.Body", e.PainterName)], button: (text["Common.OpenAdmin"], e.AdminUrl)),
            AccountDeletionRequestedEmail e => Notice(text, e, text["AccountDeletion.Subject"],
                paragraphs: [text.Format("AccountDeletion.Body", e.ClientName, e.ClientEmail)], button: (text["Common.OpenAdmin"], e.AdminUrl)),
            PriceExpiringEmail e => Notice(text, e, text.Format("PriceExpiring.Subject", e.Code), greeting: e.Name,
                paragraphs: [text.Format("PriceExpiring.Body", Date(text, e.ValidUntil))],
                rows: [(text["Common.Code"], e.Code), (text["PriceExpiring.Deposit"], Euros(text, e.DepositCents))],
                button: (text["Common.SeeJob"], e.JobUrl)),
            OverdueReviewsEmail e => Notice(text, e, text.Format("OverdueReviews.Subject", e.Codes.Count),
                paragraphs: [text["OverdueReviews.Body"], string.Join(", ", e.Codes)], button: (text["Common.OpenAdmin"], e.AdminUrl)),
            PainterDocumentsExpiringEmail e => Notice(text, e, text.Format("DocumentsExpiring.Subject", e.Lines.Count),
                paragraphs: [text["DocumentsExpiring.Body"], .. e.Lines], button: (text["Common.OpenAdmin"], e.AdminUrl)),
            IncidentReportedEmail e => Notice(text, e, text.Format("Incident.Subject", e.Code),
                paragraphs: [text.Format("Incident.Body", e.PainterName)], quote: e.Text, button: (text["Common.OpenAdmin"], e.AdminUrl)),
            _ => throw new NotSupportedException($"No template for {email.GetType().Name}."),
        };
    }

    private (string Subject, string Html) Notice(
        EmailText text, Email email, string subject, string? greeting = null, IReadOnlyList<string>? paragraphs = null,
        IReadOnlyList<(string, string)>? rows = null, string? quote = null, (string Text, string Url)? button = null) =>
        (subject, RenderAsync<NoticeTemplate>(new Dictionary<string, object?>
        {
            [nameof(NoticeTemplate.Language)] = email.Locale.ToString().ToLowerInvariant(),
            [nameof(NoticeTemplate.Subject)] = subject,
            [nameof(NoticeTemplate.Greeting)] = greeting is null ? null : text.Format("Common.Greeting", greeting),
            [nameof(NoticeTemplate.Paragraphs)] = paragraphs ?? [],
            [nameof(NoticeTemplate.Rows)] = rows ?? [],
            [nameof(NoticeTemplate.Quote)] = quote,
            [nameof(NoticeTemplate.ButtonText)] = button?.Text,
            [nameof(NoticeTemplate.ButtonUrl)] = button?.Url,
        }).GetAwaiter().GetResult());

    // Money and dates as in the web app: "1.270,00 €" in pt and es (docs/integrations.md), dates in Lisbon.
    private static CultureInfo Numbers(EmailText text) => CultureInfo.GetCultureInfo(text.Culture.TwoLetterISOLanguageName == "en" ? "en-GB" : "es-ES");

    private static string Euros(EmailText text, long cents) => $"{(cents / 100m).ToString("N2", Numbers(text))} €";

    private static string Date(EmailText text, DateOnly date) => date.ToString("d MMMM yyyy", text.Culture);

    private static string DateTime(EmailText text, DateTimeOffset at) =>
        TimeZoneInfo.ConvertTime(at, Pinta.SharedKernel.Time.BusinessDays.Lisbon).ToString("d MMMM yyyy, HH:mm", text.Culture);

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
