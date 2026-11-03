using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Notifications.Data;
using Pinta.Modules.Notifications.Features.Outbox;
using Pinta.Modules.Notifications.Features.SendEmail;
using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Notifications;

public static class NotificationsModule
{
    public static IHostApplicationBuilder AddNotificationsModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddModuleDbContext<NotificationsDbContext>(NotificationsDbContext.Schema);
        builder.Services.AddOptions<SmtpOptions>()
            .Configure(options => options.Configure(builder.Configuration))
            .ValidateOnStart();
        builder.Services.AddScoped<EmailRenderer>();
        builder.Services.AddScoped<IEmailQueue, HangfireEmailQueue>();
        if (MailtrapApiOptions.IsSelected(builder.Configuration))
        {
            builder.Services.AddOptions<MailtrapApiOptions>()
                .Bind(builder.Configuration.GetSection("Email:MailtrapApi"))
                .Validate(o => !string.IsNullOrEmpty(o.Token) && o.InboxId > 0, "Email:MailtrapApi:Token and InboxId are required.")
                .ValidateOnStart();
            builder.Services.AddHttpClient<IMailTransport, MailtrapApiEmailSender>((services, http) =>
                http.Timeout = services.GetRequiredService<IOptions<MailtrapApiOptions>>().Value.Timeout);
        }
        else
        {
            builder.Services.AddTransient<IMailTransport, SmtpEmailSender>();
        }

        builder.Services.AddScoped<OutboxSender>();
        builder.Services.AddScoped<IEmailLog, EmailLog>();
        return builder;
    }

    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app) => app;
}
