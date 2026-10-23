using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Pinta.Api.Infrastructure;
using Pinta.Api.Seed;
using Pinta.Modules.Catalog;
using Pinta.Modules.Finance;
using Pinta.Modules.Identity;
using Pinta.Modules.Identity.Data;
using Pinta.Modules.Jobs;
using Pinta.Modules.Media;
using Pinta.Modules.Notifications;
using Pinta.Modules.Painters;
using Pinta.Modules.Payments;
using Pinta.Modules.Quoting;
using Pinta.SharedKernel.Events;
using Pinta.SharedKernel.Idempotency;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
// writeToProviders keeps the OpenTelemetry logger from ServiceDefaults, so logs reach the Aspire dashboard.
builder.Services.AddSerilog(
    (services, logger) => logger
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj} {TraceId}{NewLine}{Exception}"),
    writeToProviders: true);

builder.AddNpgsqlDataSource("pinta");
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsSetup.Customize);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
    // Numbers travel as JSON numbers only (amounts in cents, measures in metres), so the OpenAPI types are plain numbers.
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});
builder.Services.AddPintaOpenApi();
builder.Services.AddRequestLocalization(options =>
{
    string[] cultures = ["pt", "es", "en"];
    options.SetDefaultCulture(cultures[0]).AddSupportedCultures(cultures).AddSupportedUICultures(cultures);
    options.ApplyCurrentCultureToResponseHeaders = true;
});
builder.AddAntiforgeryHeader();
builder.AddRateLimits();
builder.Services.AddIntegrationEvents();
builder.Services.AddIdempotency();
builder.AddHangfireJobs();

builder.AddIdentityModule();
builder.AddCatalogModule();
builder.AddQuotingModule();
builder.AddJobsModule();
builder.AddPaintersModule();
builder.AddPaymentsModule();
builder.AddMediaModule();
builder.AddNotificationsModule();
builder.AddFinanceModule();

// Keys in PostgreSQL so sessions survive restarts (docs/integrations.md); not while the OpenAPI document is generated on build.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("Pinta");
if (!OpenApiGeneration.IsRunning)
{
    dataProtection.PersistKeysToDbContext<IdentityModuleDbContext>();
}

var app = builder.Build();

// First, so error responses written on the way out still see the request language.
app.UseRequestLocalization();
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    // A body or parameter that cannot be bound (malformed JSON, unknown enum value) is the client's fault: 400 bad_request, not 500.
    StatusCodeSelector = exception => exception is BadHttpRequestException bad ? bad.StatusCode : StatusCodes.Status500InternalServerError,
    SuppressDiagnosticsCallback = context => context.Exception is BadHttpRequestException,
});
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<AntiforgeryValidationMiddleware>();

app.MapDefaultEndpoints();
app.MapOpenApi();
app.MapHangfireAdminDashboard();

app.MapIdentityEndpoints();
app.MapCatalogEndpoints();
app.MapQuotingEndpoints();
app.MapJobsEndpoints();
app.MapPaintersEndpoints();
app.MapPaymentsEndpoints();
app.MapMediaEndpoints();
app.MapNotificationsEndpoints();
app.MapFinanceEndpoints();

if (app.Environment.IsDevelopment())
{
    await app.Services.MigrateModulesAsync();
    await DemoSeeder.SeedAsync(app.Services);
}

app.Services.ScheduleRecurringJobs();

app.Run();

public partial class Program;
