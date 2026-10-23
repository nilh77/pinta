var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("pinta-postgres")
    .WithLifetime(ContainerLifetime.Persistent);
var database = postgres.AddDatabase("pinta");

// Object storage for photos and videos (Media module, H3). MinIO no longer publishes images on Docker Hub or
// quay.io, so the toolkit's default image cannot be pulled; Chainguard's free build only offers `latest`.
var minio = builder.AddMinioContainer("minio")
    .WithImageRegistry("cgr.dev")
    .WithImage("chainguard/minio", "latest")
    .WithDataVolume("pinta-minio")
    .WithLifetime(ContainerLifetime.Persistent);

var mailpit = builder.AddMailPit("mailpit");

var api = builder.AddProject<Projects.Pinta_Api>("api")
    .WithReference(database).WaitFor(database)
    .WithReference(mailpit).WaitFor(mailpit)
    .WithReference(minio).WaitFor(minio);

var web = builder.AddJavaScriptApp("web", "../../web", "dev")
    .WithPnpm()
    .WithHttpEndpoint(env: "PORT")
    .WithEnvironment("API_URL", api.GetEndpoint("http"))
    .WithExternalHttpEndpoints()
    .WaitFor(api);

// Email links point to the web app, which proxies /api to the API.
api.WithEnvironment("Web__BaseUrl", web.GetEndpoint("http"));

AddStripeCli(builder, api);

builder.Build().Run();

// Forwards Stripe test webhooks to the API when the Stripe CLI is installed and a test key is set:
//   dotnet user-secrets set "Parameters:stripe-secret-key" "sk_test_..." --project src/Pinta.AppHost
static void AddStripeCli(IDistributedApplicationBuilder builder, IResourceBuilder<ProjectResource> api)
{
    var stripe = (Environment.GetEnvironmentVariable("PATH") ?? "")
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .Select(directory => Path.Combine(directory, OperatingSystem.IsWindows() ? "stripe.exe" : "stripe"))
        .FirstOrDefault(File.Exists);
    var hasKey = !string.IsNullOrEmpty(builder.Configuration["Parameters:stripe-secret-key"]);

    if (stripe is null || !hasKey)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(stripe is null
            ? "warn: Stripe CLI not found on PATH; skipping webhook forwarding (needed from H5)."
            : "warn: Parameters:stripe-secret-key is not set in the AppHost user-secrets; skipping the Stripe CLI.");
        Console.ResetColor();
        return;
    }

    var secretKey = builder.AddParameter("stripe-secret-key", secret: true);
    builder.AddExecutable("stripe", stripe, ".")
        .WithArgs(context =>
        {
            context.Args.Add("listen");
            // Newer Stripe CLIs forward nothing unless told which events; these are the ones the webhook handles (S4).
            context.Args.Add("--events");
            context.Args.Add("payment_intent.succeeded,payment_intent.payment_failed,payment_intent.requires_action,charge.refunded,refund.updated,charge.dispute.created");
            context.Args.Add("--forward-to");
            context.Args.Add(ReferenceExpression.Create($"{api.GetEndpoint("http")}/api/webhooks/stripe"));
        })
        .WithEnvironment("STRIPE_API_KEY", secretKey)
        .WaitFor(api);
}
