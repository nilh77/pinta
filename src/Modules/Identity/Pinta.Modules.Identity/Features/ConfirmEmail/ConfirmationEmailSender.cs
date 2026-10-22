using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Identity.Domain;
using Pinta.Modules.Notifications.Contracts;

namespace Pinta.Modules.Identity.Features.ConfirmEmail;

/// <summary>Generates the confirmation token (valid 48 h) and queues the email with a link to the web app (C4.5).</summary>
internal sealed class ConfirmationEmailSender(UserManager<User> users, IEmailQueue emails, IConfiguration configuration) : IEmailConfirmationLinks
{
    public async Task SendAsync(User user, CancellationToken cancellationToken)
    {
        var url = await CreateLinkAsync(user);
        await emails.EnqueueAsync(new ConfirmEmailEmail(user.Email!, user.Locale, user.Name, url), cancellationToken);
    }

    public async Task<string> CreateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException($"User {userId} not found.");
        return await CreateLinkAsync(user);
    }

    private async Task<string> CreateLinkAsync(User user)
    {
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var webUrl = (configuration["Web:BaseUrl"] ?? "http://localhost:3000").TrimEnd('/');
        var locale = user.Locale.ToString().ToLowerInvariant();
        return $"{webUrl}/{locale}/confirmar-email?userId={user.Id}&token={encodedToken}";
    }

    public static string? DecodeToken(string encodedToken)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
