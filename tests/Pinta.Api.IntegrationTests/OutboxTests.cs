using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Notifications.Data;
using Pinta.Modules.Notifications.Domain;
using Pinta.Modules.Notifications.Features.Outbox;
using Pinta.Modules.Notifications.Features.SendEmail;
using Pinta.SharedKernel;

namespace Pinta.Api.IntegrationTests;

/// <summary>S2: every email is rendered once, recorded in notifications.outbox and sent by Hangfire with retries.</summary>
public class OutboxTests(PintaApiFactory factory)
{
    private static readonly PriceConfirmedEmail PriceConfirmed =
        new("c@test.pt", Locale.Pt, "Ana", "PT-2026-0001", 318468, 95540, new DateOnly(2026, 11, 6), "Inclui o anti-humidade.", "http://web.test/pt/mis-trabajos/1");

    public static TheoryData<Email> H4Emails => new()
    {
        PriceConfirmed,
        new RequestRejectedEmail("c@test.pt", Locale.Es, "Ana", "PT-2026-0001", "Fuera de nuestro alcance."),
        new PriceExpiredEmail("c@test.pt", Locale.En, "Ana", "PT-2026-0001", "http://web.test/en/mis-trabajos/1"),
        new SiteVisitScheduledEmail("c@test.pt", Locale.Pt, "Ana", "PT-2026-0001", new DateTimeOffset(2026, 10, 27, 9, 30, 0, TimeSpan.Zero), "Nathan", 3000),
        new ClientQuestionEmail("admin@test.pt", Locale.Es, "PT-2026-0001", "Ana", "c@test.pt", "¿Incluye el techo?", "http://web.test/es/admin/trabajos/1"),
        new ClientRejectedPriceEmail("admin@test.pt", Locale.En, "PT-2026-0001", "Ana", null, "http://web.test/en/admin/trabajos/1"),
        new ResetPasswordEmail("c@test.pt", Locale.Pt, "Ana", "http://web.test/pt/restablecer?token=x"),
    };

    [Theory]
    [MemberData(nameof(H4Emails))]
    public async Task S2_every_H4_email_renders_in_its_language(Email email)
    {
        using var scope = factory.Services.CreateScope();
        var (subject, html) = await scope.ServiceProvider.GetRequiredService<EmailRenderer>().RenderAsync(email);

        Assert.False(string.IsNullOrWhiteSpace(subject));
        Assert.Contains($"lang=\"{email.Locale.ToString().ToLowerInvariant()}\"", html);
        if (email is not ResetPasswordEmail)
        {
            Assert.Contains("PT-2026-0001", subject);
        }
    }

    [Fact]
    public async Task S2_price_confirmed_shows_money_and_dates_in_portuguese_format()
    {
        using var scope = factory.Services.CreateScope();
        var (_, encoded) = await scope.ServiceProvider.GetRequiredService<EmailRenderer>().RenderAsync(PriceConfirmed);
        var html = System.Net.WebUtility.HtmlDecode(encoded);

        Assert.Contains("3.184,68 €", html);
        Assert.Contains("955,40 €", html);
        Assert.Contains("6 novembro 2026", html);
    }

    [Fact]
    public async Task S2_queueing_records_the_email_and_enqueues_its_sending()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var jobs = new RecordingJobClient();
        var queue = new HangfireEmailQueue(scope.ServiceProvider.GetRequiredService<EmailRenderer>(), db, jobs, TimeProvider.System);
        var jobId = Guid.CreateVersion7();

        await queue.EnqueueAsync(new RequestRejectedEmail("queued@test.pt", Locale.Pt, "Ana", "PT-2026-0002", "Motivo") { JobId = jobId });

        var row = await db.Outbox.AsNoTracking().SingleAsync(e => e.JobId == jobId);
        Assert.Equal(("queued@test.pt", "RequestRejected", OutgoingEmailStatus.Queued, 0), (row.To, row.Type, row.Status, row.Attempts));
        var job = Assert.Single(jobs.Created);
        Assert.Equal((typeof(OutboxSender), nameof(OutboxSender.SendAsync)), (job.Type, job.Method.Name));
        Assert.Equal(row.Id, job.Args[0]);

        var log = await scope.ServiceProvider.GetRequiredService<IEmailLog>().ListForJobAsync(jobId);
        Assert.Equal("queued", Assert.Single(log).Status);
    }

    [Fact]
    public async Task S2_a_failed_attempt_is_recorded_and_rethrown_so_hangfire_retries_it()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var email = new OutgoingEmail { To = "retry@test.pt", Type = "PriceExpired", Subject = "S", Html = "<p>x</p>", CreatedAt = TimeProvider.System.GetUtcNow() };
        db.Outbox.Add(email);
        await db.SaveChangesAsync();
        var transport = new FlakyTransport(failures: 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => new OutboxSender(db, transport, TimeProvider.System).SendAsync(email.Id));
        var failed = await db.Outbox.AsNoTracking().SingleAsync(e => e.Id == email.Id);
        Assert.Equal((OutgoingEmailStatus.Failed, 1, "SMTP down"), (failed.Status, failed.Attempts, failed.LastError));

        await new OutboxSender(db, transport, TimeProvider.System).SendAsync(email.Id);
        var sent = await db.Outbox.AsNoTracking().SingleAsync(e => e.Id == email.Id);
        Assert.Equal((OutgoingEmailStatus.Sent, 2, null), (sent.Status, sent.Attempts, sent.LastError));
        Assert.NotNull(sent.SentAt);

        // A retry after success does not send twice.
        await new OutboxSender(db, transport, TimeProvider.System).SendAsync(email.Id);
        Assert.Equal(1, transport.Sent);
    }

    [Fact]
    public void S2_sending_retries_up_to_five_times()
    {
        var retry = typeof(OutboxSender).GetMethod(nameof(OutboxSender.SendAsync))!.GetCustomAttributes(typeof(AutomaticRetryAttribute), false);
        Assert.Equal(5, Assert.IsType<AutomaticRetryAttribute>(Assert.Single(retry)).Attempts);
    }

    private sealed class RecordingJobClient : IBackgroundJobClient
    {
        public List<Job> Created { get; } = [];

        public string Create(Job job, IState state)
        {
            Created.Add(job);
            return Created.Count.ToString();
        }

        public bool ChangeState(string jobId, IState state, string expectedState) => true;
    }

    private sealed class FlakyTransport(int failures) : IMailTransport
    {
        private int _failures = failures;

        public int Sent { get; private set; }

        public Task SendAsync(string to, string subject, string html, CancellationToken cancellationToken)
        {
            if (_failures-- > 0)
            {
                throw new InvalidOperationException("SMTP down");
            }

            Sent++;
            return Task.CompletedTask;
        }
    }
}
