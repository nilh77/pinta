using FluentValidation;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pinta.Modules.Identity.Contracts;
using Pinta.Modules.Identity.Data;
using Pinta.Modules.Identity.Domain;
using Pinta.Modules.Identity.Features.Antiforgery;
using Pinta.Modules.Identity.Features.ConfirmEmail;
using Pinta.Modules.Identity.Features.Google;
using Pinta.Modules.Identity.Features.Login;
using Pinta.Modules.Identity.Features.Logout;
using Pinta.Modules.Identity.Features.Me;
using Pinta.Modules.Identity.Features.PasswordReset;
using Pinta.Modules.Identity.Features.Register;
using Pinta.Modules.Identity.Features.ResendConfirmation;
using Pinta.SharedKernel.Data;

namespace Pinta.Modules.Identity;

public static class IdentityModule
{
    public const string SessionCookieName = "pinta.session";

    public static IHostApplicationBuilder AddIdentityModule(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddModuleDbContext<IdentityModuleDbContext>(IdentityModuleDbContext.Schema);

        services.AddIdentityCore<User>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = RegisterRequestValidator.MinPasswordLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
                // Clients can sign in and submit a request before confirming (C4.4).
                options.SignIn.RequireConfirmedEmail = false;
                options.Tokens.PasswordResetTokenProvider = PasswordResetTokenProvider.ProviderName;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<IdentityModuleDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddTokenProvider<PasswordResetTokenProvider>(PasswordResetTokenProvider.ProviderName);
        services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(48));

        var authentication = services.AddAuthentication(IdentityConstants.ApplicationScheme);
        authentication.AddIdentityCookies();
        if (GoogleLoginEndpoints.IsConfigured(builder.Configuration))
        {
            authentication.AddGoogle(GoogleLoginEndpoints.Scheme, options =>
            {
                options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
                options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
                options.SignInScheme = IdentityConstants.ExternalScheme;
                // Under /api, which the web app proxies to the API.
                options.CallbackPath = GoogleLoginEndpoints.CallbackPath;
            });
        }
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = SessionCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
            // An API answers 401/403 (turned into ProblemDetails by the host) instead of redirecting.
            options.Events = new CookieAuthenticationEvents
            {
                OnRedirectToLogin = context => SetStatus(context.Response, StatusCodes.Status401Unauthorized),
                OnRedirectToAccessDenied = context => SetStatus(context.Response, StatusCodes.Status403Forbidden),
            };
        });
        services.AddAuthorization();

        services.AddValidatorsFromAssemblyContaining<RegisterRequest>(includeInternalTypes: true);
        services.AddScoped<ConfirmationEmailSender>();
        services.AddScoped<IEmailConfirmationLinks>(sp => sp.GetRequiredService<ConfirmationEmailSender>());
        services.AddScoped<IUserDirectory, UserDirectory>();
        return builder;
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Auth");
        AntiforgeryEndpoint.Map(auth);
        RegisterEndpoint.Map(auth);
        LoginEndpoint.Map(auth);
        LogoutEndpoint.Map(auth);
        MeEndpoint.Map(auth);
        ConfirmEmailEndpoint.Map(auth);
        ResendConfirmationEndpoint.Map(auth);
        PasswordResetEndpoints.Map(auth);
        GoogleLoginEndpoints.Map(auth);
        return app;
    }

    private static Task SetStatus(HttpResponse response, int status)
    {
        response.StatusCode = status;
        return Task.CompletedTask;
    }
}
