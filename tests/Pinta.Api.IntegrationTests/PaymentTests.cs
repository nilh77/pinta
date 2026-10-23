using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pinta.Modules.Jobs.Data;
using Pinta.Modules.Notifications.Contracts;
using Pinta.Modules.Payments.Data;
using Pinta.Modules.Payments.Features.Pay;
using Pinta.Modules.Payments.Contracts;

namespace Pinta.Api.IntegrationTests;

public static class TestPayments
{
    public static async Task<HttpResponseMessage> CreateAsync(HttpClient client, Guid jobId, string type = "deposit")
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/jobs/{jobId}/payments") { Content = JsonContent.Create(new { type }, options: ApiClient.Json) };
        message.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(message);
    }

    /// <summary>The PaymentIntent behind a client secret ("pi_…_secret_…").</summary>
    public static string IntentId(JsonElement created) => created.GetProperty("clientSecret").GetString()!.Split("_secret_")[0];

    /// <summary>A TC01 job with a confirmed price and its deposit paid through the webhook.</summary>
    public static async Task<(TestJobs.SentJob Job, string IntentId)> PaidDepositAsync(PintaApiFactory factory)
    {
        var job = await TestJobs.SendAsync(factory);
        await TestReview.ConfirmAsync(factory, job.JobId);
        var created = await (await CreateAsync(job.Client, job.JobId)).ReadJsonAsync();
        var intent = IntentId(created);
        var webhook = await FakeStripe.SendAsync(factory.CreateClient(), "payment_intent.succeeded", FakeStripe.Succeeded(intent, 14768));
        Assert.Equal(HttpStatusCode.OK, webhook.StatusCode);
        return (job, intent);
    }
}

/// <summary>C5.3 and S4: the deposit is charged by Stripe and confirmed only by a signed, idempotent webhook.</summary>
public class PaymentTests(PintaApiFactory factory)
{
    private async Task<string> StatusAsync(TestJobs.SentJob job) =>
        (await (await job.Client.GetAsync($"/api/jobs/{job.JobId}")).ReadJsonAsync()).GetProperty("status").GetString()!;

    [Fact]
    public async Task C5_3_S4_TC01_deposit_moves_the_job_only_after_the_webhook_and_a_replay_changes_nothing()
    {
        var job = await TestJobs.SendAsync(factory);
        await TestReview.ConfirmAsync(factory, job.JobId);

        var response = await TestPayments.CreateAsync(job.Client, job.JobId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await response.ReadJsonAsync();
        Assert.Equal((14768, "pk_test_fake", false), (created.GetProperty("amountCents").GetInt64(), created.GetProperty("publishableKey").GetString(), created.GetProperty("mbWayLimitWarning").GetBoolean()));
        Assert.Equal(["card", "mb_way", "multibanco"], created.GetProperty("methods").EnumerateArray().Select(m => m.GetString()));
        var intent = TestPayments.IntentId(created);
        Assert.Equal(["card", "mb_way", "multibanco"], factory.Stripe.MethodTypes[intent]);
        Assert.Equal("quoted", await StatusAsync(job));

        var eventId = $"evt_{Guid.NewGuid():N}";
        var webhook = factory.CreateClient();
        var first = await FakeStripe.SendAsync(webhook, "payment_intent.succeeded", FakeStripe.Succeeded(intent, 14768), eventId);
        var replay = await FakeStripe.SendAsync(webhook, "payment_intent.succeeded", FakeStripe.Succeeded(intent, 14768), eventId);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (first.StatusCode, replay.StatusCode));
        Assert.Equal("deposit_paid", await StatusAsync(job));
        var detail = await (await job.Client.GetAsync($"/api/jobs/{job.JobId}")).ReadJsonAsync();
        Assert.Single(detail.GetProperty("timeline").EnumerateArray(), e => e.GetProperty("status").GetString() == "deposit_paid");
        var email = Assert.Single(factory.Emails.To<PaymentReceivedEmail>(job.Email));
        Assert.Equal(("deposit", 14768, false), (email.Kind, email.AmountCents, email.MbWay));

        var payment = Assert.Single((await (await job.Client.GetAsync($"/api/jobs/{job.JobId}/payments")).ReadJsonAsync()).EnumerateArray());
        Assert.Equal(("succeeded", "card", "deposit"), (payment.GetProperty("status").GetString(), payment.GetProperty("method").GetString(), payment.GetProperty("type").GetString()));
        Assert.Matches(@"^SIM-\d{4}-\d{4}$", payment.GetProperty("receiptNumber").GetString());
        await ProblemAssert.HasCodeAsync(await TestPayments.CreateAsync(job.Client, job.JobId), HttpStatusCode.UnprocessableEntity, "payment_already_made");
    }

    [Fact]
    public async Task S4_a_webhook_with_a_bad_signature_is_rejected()
    {
        var response = await FakeStripe.SendAsync(factory.CreateClient(), "payment_intent.succeeded", FakeStripe.Succeeded("pi_x", 100), secret: "whsec_wrong");

        await ProblemAssert.HasCodeAsync(response, HttpStatusCode.BadRequest, "bad_request");
    }

    [Fact]
    public async Task C5_3_going_back_reuses_the_same_payment_intent()
    {
        var job = await TestJobs.SendAsync(factory);
        await TestReview.ConfirmAsync(factory, job.JobId);

        var first = await (await TestPayments.CreateAsync(job.Client, job.JobId)).ReadJsonAsync();
        var second = await (await TestPayments.CreateAsync(job.Client, job.JobId)).ReadJsonAsync();

        Assert.Equal(first.GetProperty("paymentId").GetGuid(), second.GetProperty("paymentId").GetGuid());
        Assert.Equal(first.GetProperty("clientSecret").GetString(), second.GetProperty("clientSecret").GetString());
    }

    [Fact]
    public async Task C5_3_no_deposit_before_the_price_or_after_it_expires_and_only_for_the_owner()
    {
        var job = await TestJobs.SendAsync(factory);
        var other = await TestJobs.SendAsync(factory);

        await ProblemAssert.HasCodeAsync(await TestPayments.CreateAsync(job.Client, job.JobId), HttpStatusCode.Conflict, "invalid_transition");
        await TestReview.ConfirmAsync(factory, job.JobId);
        await ProblemAssert.HasCodeAsync(await TestPayments.CreateAsync(other.Client, job.JobId), HttpStatusCode.NotFound, "not_found");
        await ProblemAssert.HasCodeAsync(await other.Client.GetAsync($"/api/jobs/{job.JobId}/payments"), HttpStatusCode.NotFound, "not_found");
        await ProblemAssert.HasCodeAsync(await TestPayments.CreateAsync(job.Client, job.JobId, "balance"), HttpStatusCode.Conflict, "invalid_transition");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
            var stored = await db.Jobs.SingleAsync(j => j.Id == job.JobId);
            stored.QuoteValidUntil = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
            await db.SaveChangesAsync();
        }

        await ProblemAssert.HasCodeAsync(await TestPayments.CreateAsync(job.Client, job.JobId), HttpStatusCode.Gone, "quote_expired");
    }

    [Fact]
    public async Task S4_a_payment_for_a_cancelled_job_is_refunded_in_full_and_the_admins_are_told()
    {
        var job = await TestJobs.SendAsync(factory);
        await TestReview.ConfirmAsync(factory, job.JobId);
        var intent = TestPayments.IntentId(await (await TestPayments.CreateAsync(job.Client, job.JobId)).ReadJsonAsync());
        await job.Client.PostAsJsonAsync($"/api/jobs/{job.JobId}/cancel", new { reason = "Mudei de ideias." }, ApiClient.Json);

        await FakeStripe.SendAsync(factory.CreateClient(), "payment_intent.succeeded", FakeStripe.Succeeded(intent, 14768));

        Assert.Equal("cancelled", await StatusAsync(job));
        Assert.Contains(factory.Emails.To<AutoRefundEmail>(TestData.AdminEmail), e => e.Code == job.Code && e.AmountCents == 14768);
        Assert.Empty(factory.Emails.To<PaymentReceivedEmail>(job.Email));
        await WaitForAsync(() => factory.Stripe.Refunds.Any(r => r.PaymentIntentId == intent && r.AmountCents == 14768));

        // charge.refunded completes it.
        var refundId = factory.Stripe.Refunds.First(r => r.PaymentIntentId == intent).IdempotencyKey;
        await WaitForStripeIdAsync(intent);
        await FakeStripe.SendAsync(factory.CreateClient(), "charge.refunded",
            new { id = "ch_1", @object = "charge", payment_intent = intent, amount = 14768, amount_refunded = 14768 });
        var payment = Assert.Single((await (await job.Client.GetAsync($"/api/jobs/{job.JobId}/payments")).ReadJsonAsync()).EnumerateArray());
        Assert.Equal(("refunded", 14768), (payment.GetProperty("status").GetString(), payment.GetProperty("refundedCents").GetInt64()));
        Assert.StartsWith("refund-", refundId);
    }

    [Fact]
    public async Task C5_3_multibanco_reference_is_stored_and_emailed_and_failures_keep_the_message()
    {
        var job = await TestJobs.SendAsync(factory);
        await TestReview.ConfirmAsync(factory, job.JobId);
        var intent = TestPayments.IntentId(await (await TestPayments.CreateAsync(job.Client, job.JobId)).ReadJsonAsync());
        var expires = DateTimeOffset.UtcNow.AddDays(3).ToUnixTimeSeconds();

        await FakeStripe.SendAsync(factory.CreateClient(), "payment_intent.requires_action", new
        {
            id = intent, @object = "payment_intent", amount = 14768, currency = "eur", status = "requires_action",
            next_action = new { type = "multibanco_display_details", multibanco_display_details = new { entity = "12345", reference = "123456789", expires_at = expires } },
        });

        var payment = Assert.Single((await (await job.Client.GetAsync($"/api/jobs/{job.JobId}/payments")).ReadJsonAsync()).EnumerateArray());
        Assert.Equal(("requires_action", "multibanco", "12345", "123456789"), (payment.GetProperty("status").GetString(), payment.GetProperty("method").GetString(),
            payment.GetProperty("multibanco").GetProperty("entity").GetString(), payment.GetProperty("multibanco").GetProperty("reference").GetString()));
        Assert.Equal("123456789", Assert.Single(factory.Emails.To<MultibancoReferenceEmail>(job.Email)).Reference);
        Assert.Equal("quoted", await StatusAsync(job));

        await FakeStripe.SendAsync(factory.CreateClient(), "payment_intent.payment_failed", new
        {
            id = intent, @object = "payment_intent", amount = 14768, currency = "eur", status = "requires_payment_method",
            last_payment_error = new { message = "Your card was declined.", type = "card_error" },
        });
        payment = Assert.Single((await (await job.Client.GetAsync($"/api/jobs/{job.JobId}/payments")).ReadJsonAsync()).EnumerateArray());
        Assert.Equal(("failed", "Your card was declined."), (payment.GetProperty("status").GetString(), payment.GetProperty("error").GetString()));
    }

    [Fact]
    public void C5_3_methods_by_amount()
    {
        Assert.Equal([PaymentMethod.Card, PaymentMethod.MbWay, PaymentMethod.Multibanco], PaymentRules.AllowedMethods(100000));
        Assert.False(PaymentRules.MbWayWarning(100000));
        Assert.True(PaymentRules.MbWayWarning(400000));
        Assert.Equal([PaymentMethod.Card, PaymentMethod.MbWay, PaymentMethod.Multibanco], PaymentRules.AllowedMethods(500000));
        Assert.Equal([PaymentMethod.Card, PaymentMethod.Multibanco], PaymentRules.AllowedMethods(500001));
        Assert.False(PaymentRules.MbWayWarning(596962));
    }

    [Fact]
    public async Task A3_5_admins_see_the_payments_of_any_job_clients_cannot_use_the_admin_route()
    {
        var (job, _) = await TestPayments.PaidDepositAsync(factory);
        var admin = await ApiClient.CreateAdminAsync(factory);

        var payments = await (await admin.GetAsync($"/api/admin/jobs/{job.JobId}/payments")).ReadJsonAsync();

        Assert.Equal("succeeded", Assert.Single(payments.EnumerateArray()).GetProperty("status").GetString());
        await ProblemAssert.HasCodeAsync(await job.Client.GetAsync($"/api/admin/jobs/{job.JobId}/payments"), HttpStatusCode.Forbidden, "forbidden");
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(100);
        }

        Assert.True(condition(), "Timed out waiting for the background job.");
    }

    private async Task WaitForStripeIdAsync(string intent)
    {
        for (var i = 0; i < 100; i++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            if (await db.Refunds.AnyAsync(r => r.StripeRefundId != null && db.Payments.Any(p => p.Id == r.PaymentId && p.StripePaymentIntentId == intent)))
            {
                return;
            }

            await Task.Delay(100);
        }

        Assert.Fail("The refund was never sent to Stripe.");
    }
}

/// <summary>Balance, site visit and the scheduled date (C5.5, A2.6, A3.3).</summary>
public class PaymentFlowTests(PintaApiFactory factory)
{
    private async Task SetStatusAsync(Guid jobId, Pinta.Modules.Jobs.Contracts.JobStatus status)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<JobsDbContext>();
        // Painters finish jobs in H6: the test puts it there directly.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE jobs.jobs SET status = {System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(status.ToString())} WHERE id = {jobId}");
    }

    [Fact]
    public async Task A3_3_moving_to_scheduled_needs_a_date_from_today()
    {
        var (job, _) = await TestPayments.PaidDepositAsync(factory);
        var admin = await ApiClient.CreateAdminAsync(factory);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var noDate = await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/transition", new { to = "scheduled", reason = "Pintor confirmado" }, ApiClient.Json);
        var past = await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/transition", new { to = "scheduled", reason = "Pintor confirmado", scheduledOn = today.AddDays(-2) }, ApiClient.Json);
        var ok = await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/transition", new { to = "scheduled", reason = "Pintor confirmado", scheduledOn = today.AddDays(10) }, ApiClient.Json);

        Assert.True((await ProblemAssert.HasCodeAsync(noDate, HttpStatusCode.UnprocessableEntity, "validation_failed")).GetProperty("errors").TryGetProperty("scheduledOn", out _));
        await ProblemAssert.HasCodeAsync(past, HttpStatusCode.UnprocessableEntity, "validation_failed");
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        var detail = await (await job.Client.GetAsync($"/api/jobs/{job.JobId}")).ReadJsonAsync();
        Assert.Equal(("scheduled", today.AddDays(10).ToString("yyyy-MM-dd")), (detail.GetProperty("status").GetString(), detail.GetProperty("scheduledOn").GetString()));
    }

    [Fact]
    public async Task C5_5_the_balance_of_a_finished_job_closes_it()
    {
        var (job, _) = await TestPayments.PaidDepositAsync(factory);
        await SetStatusAsync(job.JobId, Pinta.Modules.Jobs.Contracts.JobStatus.Finished);

        var created = await (await TestPayments.CreateAsync(job.Client, job.JobId, "balance")).ReadJsonAsync();
        Assert.Equal(34457, created.GetProperty("amountCents").GetInt64());
        await FakeStripe.SendAsync(factory.CreateClient(), "payment_intent.succeeded", FakeStripe.Succeeded(TestPayments.IntentId(created), 34457));

        Assert.Equal("closed", (await (await job.Client.GetAsync($"/api/jobs/{job.JobId}")).ReadJsonAsync()).GetProperty("status").GetString());
        Assert.Contains(factory.Emails.To<PaymentReceivedEmail>(job.Email), e => e.Kind == "balance" && e.AmountCents == 34457);
    }

    [Fact]
    public async Task A2_6_TC15_a_paid_deductible_visit_comes_off_the_next_price()
    {
        var job = await TestJobs.SendAsync(factory, TestJobs.Flat, "furnished");
        var admin = await ApiClient.CreateAdminAsync(factory);
        var partner = (await (await admin.GetAsync("/api/admin/partners")).ReadJsonAsync()).EnumerateArray().First().GetProperty("id").GetGuid();
        await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/site-visit", new { assigneeId = partner, at = DateTimeOffset.UtcNow.AddDays(2), priceCents = 5000 }, ApiClient.Json);

        var visit = await (await TestPayments.CreateAsync(job.Client, job.JobId, "site_visit")).ReadJsonAsync();
        Assert.Equal(5000, visit.GetProperty("amountCents").GetInt64());
        await FakeStripe.SendAsync(factory.CreateClient(), "payment_intent.succeeded", FakeStripe.Succeeded(TestPayments.IntentId(visit), 5000));
        Assert.Equal("site_visit", (await (await job.Client.GetAsync($"/api/jobs/{job.JobId}")).ReadJsonAsync()).GetProperty("status").GetString());

        await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/site-visit/complete", new { }, ApiClient.Json);
        var quote = (await (await admin.GetAsync($"/api/admin/jobs/{job.JobId}")).ReadJsonAsync()).GetProperty("currentQuote");
        var body = new System.Text.Json.Nodes.JsonObject
        {
            ["occupancy"] = "furnished",
            ["elements"] = System.Text.Json.Nodes.JsonNode.Parse(quote.GetProperty("elements").GetRawText()),
            ["extras"] = System.Text.Json.Nodes.JsonNode.Parse("""[{ "catalogKey": "anti_damp_treatment" }]"""),
        };
        var draft = await (await admin.PutAsJsonAsync($"/api/admin/jobs/{job.JobId}/draft-quote", body, ApiClient.Json)).ReadJsonAsync();
        Assert.Equal((313468, 5000), (draft.GetProperty("breakdown").GetProperty("totalCents").GetInt64(), draft.GetProperty("siteVisitCreditCents").GetInt64()));

        await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/confirm-quote", new { acknowledgeLowMargin = true }, ApiClient.Json);
        var client = (await (await job.Client.GetAsync($"/api/jobs/{job.JobId}")).ReadJsonAsync()).GetProperty("quote");
        Assert.Equal((313468, 94040), (client.GetProperty("totalCents").GetInt64(), client.GetProperty("depositCents").GetInt64()));
        Assert.Equal([("work", 303468L), ("extra", 15000L), ("site_visit", -5000L)],
            client.GetProperty("lines").EnumerateArray().Select(l => (l.GetProperty("kind").GetString(), l.GetProperty("amountCents").GetInt64())));
    }
}

/// <summary>C5.6, A3.4: cancelling refunds the deposit under the policy through Stripe.</summary>
public class RefundTests(PintaApiFactory factory)
{
    private async Task<TestJobs.SentJob> ScheduledInAsync(int days)
    {
        var (job, _) = await TestPayments.PaidDepositAsync(factory);
        var admin = await ApiClient.CreateAdminAsync(factory);
        var response = await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/transition",
            new { to = "scheduled", reason = "Pintor confirmado", scheduledOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(days) }, ApiClient.Json);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return job;
    }

    private async Task<(string IntentId, long AmountCents)> SentRefundAsync(Guid jobId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var intent = (await db.Payments.SingleAsync(p => p.JobId == jobId && p.Type == PaymentType.Deposit)).StripePaymentIntentId;
        for (var i = 0; i < 100 && !factory.Stripe.Refunds.Any(r => r.PaymentIntentId == intent); i++)
        {
            await Task.Delay(100);
        }

        return Assert.Single(factory.Stripe.Refunds.Where(r => r.PaymentIntentId == intent).Select(r => (r.PaymentIntentId, r.AmountCents)));
    }

    [Fact]
    public async Task C5_6_TC01_cancelling_3_days_before_refunds_half_the_deposit()
    {
        var job = await ScheduledInAsync(3);

        var preview = await (await job.Client.GetAsync($"/api/jobs/{job.JobId}/cancellation-preview")).ReadJsonAsync();
        var cancelled = await job.Client.PostAsJsonAsync($"/api/jobs/{job.JobId}/cancel", new { reason = "Mudança de planos." }, ApiClient.Json);

        Assert.Equal((true, 14768L, 7384L, 3), (preview.GetProperty("cancellable").GetBoolean(), preview.GetProperty("depositPaidCents").GetInt64(),
            preview.GetProperty("refundCents").GetInt64(), preview.GetProperty("daysBeforeStart").GetInt32()));
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        var (intent, amount) = await SentRefundAsync(job.JobId);
        Assert.Equal(7384, amount);
        Assert.Equal(7384, Assert.Single(factory.Emails.To<RefundEmail>(job.Email)).AmountCents);

        await FakeStripe.SendAsync(factory.CreateClient(), "charge.refunded",
            new { id = $"ch_{Guid.NewGuid():N}", @object = "charge", payment_intent = intent, amount = 14768, amount_refunded = 7384 });
        var payment = Assert.Single((await (await job.Client.GetAsync($"/api/jobs/{job.JobId}/payments")).ReadJsonAsync()).EnumerateArray());
        Assert.Equal(("partially_refunded", 7384), (payment.GetProperty("status").GetString(), payment.GetProperty("refundedCents").GetInt64()));
    }

    [Fact]
    public async Task C5_6_cancelling_8_days_before_refunds_the_whole_deposit()
    {
        var job = await ScheduledInAsync(8);

        await job.Client.PostAsJsonAsync($"/api/jobs/{job.JobId}/cancel", new { }, ApiClient.Json);

        Assert.Equal(14768, (await SentRefundAsync(job.JobId)).AmountCents);
    }

    [Fact]
    public async Task A3_4_the_admin_can_refund_everything_as_an_exception()
    {
        var job = await ScheduledInAsync(2);
        var admin = await ApiClient.CreateAdminAsync(factory);

        var policy = await (await admin.GetAsync($"/api/admin/jobs/{job.JobId}/cancellation-preview")).ReadJsonAsync();
        var full = await (await admin.GetAsync($"/api/admin/jobs/{job.JobId}/cancellation-preview?fullRefund=true")).ReadJsonAsync();
        await admin.PostAsJsonAsync($"/api/admin/jobs/{job.JobId}/cancel", new { reason = "Erro nosso", fullRefund = true }, ApiClient.Json);

        Assert.Equal((7384, 14768), (policy.GetProperty("refundCents").GetInt64(), full.GetProperty("refundCents").GetInt64()));
        Assert.Equal(14768, (await SentRefundAsync(job.JobId)).AmountCents);
    }

    [Fact]
    public async Task C5_6_cancelling_closes_payments_still_open()
    {
        var job = await TestJobs.SendAsync(factory);
        await TestReview.ConfirmAsync(factory, job.JobId);
        var intent = TestPayments.IntentId(await (await TestPayments.CreateAsync(job.Client, job.JobId)).ReadJsonAsync());

        await job.Client.PostAsJsonAsync($"/api/jobs/{job.JobId}/reject-quote", new { }, ApiClient.Json);

        Assert.Contains(intent, factory.Stripe.Cancelled);
        var payment = Assert.Single((await (await job.Client.GetAsync($"/api/jobs/{job.JobId}/payments")).ReadJsonAsync()).EnumerateArray());
        Assert.Equal("cancelled", payment.GetProperty("status").GetString());
        Assert.Empty(factory.Emails.To<RefundEmail>(job.Email));
    }
}
