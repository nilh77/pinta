using FluentValidation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Pinta.Modules.Payments.Contracts;
using Pinta.Modules.Payments.Data;
using Pinta.Modules.Payments.Features;
using Pinta.Modules.Payments.Features.Pay;
using Pinta.Modules.Payments.Features.Refunds;
using Pinta.Modules.Payments.Features.Webhooks;
using Pinta.Modules.Payments.Stripe;
using Pinta.Modules.Jobs.Contracts;
using Pinta.SharedKernel.Data;
using Pinta.SharedKernel.Events;
using Pinta.SharedKernel.Http;

namespace Pinta.Modules.Payments;

public static class PaymentsModule
{
    public static IHostApplicationBuilder AddPaymentsModule(this IHostApplicationBuilder builder)
    {
        builder.Services.AddModuleDbContext<PaymentsDbContext>(PaymentsDbContext.Schema);
        builder.Services.AddSingleton(StripeOptions.FromConfiguration(builder.Configuration));
        builder.Services.AddSingleton<IStripeGateway, StripeGateway>();
        builder.Services.TryAddSingleton<WebLinks>();
        builder.Services.AddScoped<WebhookProcessor>();
        builder.Services.AddScoped<RefundSender>();
        builder.Services.AddScoped<IJobPayments, JobPayments>();
        builder.Services.AddEventHandler<JobCancelled, RefundOnCancellation>();
        builder.Services.AddValidatorsFromAssemblyContaining<CreatePaymentRequest>(includeInternalTypes: true);
        return builder;
    }

    public static IEndpointRouteBuilder MapPaymentsEndpoints(this IEndpointRouteBuilder app)
    {
        PaymentEndpoints.Map(app);
        StripeWebhookEndpoint.Map(app);
        return app;
    }
}
