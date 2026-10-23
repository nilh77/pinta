using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        builder.Services.AddTransient<IMailTransport, SmtpEmailSender>();
        builder.Services.AddScoped<OutboxSender>();
        builder.Services.AddScoped<IEmailLog, EmailLog>();
        return builder;
    }

    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app) => app;
}
