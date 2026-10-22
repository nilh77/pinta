using Microsoft.Extensions.DependencyInjection;
using Pinta.Modules.Quoting.Contracts;
using Pinta.Pricing;
using Pinta.SharedKernel.Time;

namespace Pinta.Api.IntegrationTests;

/// <summary>Review drafts, confirmation and the client's view of the price (A2.3–A2.8, C5.2), on the quoting service.</summary>
public class QuoteReviewTests(PintaApiFactory factory)
{
    private static QuoteElementRequest Room(string type, string paint, decimal? length = null, decimal? width = null,
        RoomSize? size = null, bool[]? walls = null, bool ceiling = true, bool damage = false, string? key = null) =>
        new(ElementKind.Room, null, type, size, length, width, null, walls ?? [true, true, true, true], ceiling, null, paint, damage, key);

    /// <summary>TC09 of docs/pricing-engine.md.</summary>
    public static readonly QuoteElementRequest[] Flat =
    [
        Room("living", "matte", 5m, 4m, key: "el-1"),
        Room("kitchen", "satin", 3.5m, 3m),
        Room("bedroom", "matte", 4m, 3.5m),
        Room("bedroom", "matte", size: RoomSize.M, walls: [true, true, true, false]),
        Room("bathroom", "anti_damp", 2.5m, 2m, damage: true),
        Room("hallway", "matte", 6m, 1.2m, ceiling: false),
    ];

    private static readonly DraftExtraRequest AntiDamp = new("anti_damp_treatment", null, null, null);

    private async Task<(IServiceScope Scope, IQuoteVersions Quotes, Guid JobId)> EstimateAsync(IReadOnlyList<QuoteElementRequest> elements, Occupancy occupancy)
    {
        var scope = factory.Services.CreateScope();
        var quotes = scope.ServiceProvider.GetRequiredService<IQuoteVersions>();
        var jobId = Guid.CreateVersion7();
        var (version, errors) = await quotes.CreateEstimateAsync(jobId, Guid.CreateVersion7(), occupancy, elements);
        Assert.Null(errors);
        Assert.NotNull(version);
        return (scope, quotes, jobId);
    }

    [Fact]
    public async Task A2_TC10_admin_adds_the_catalog_extra_confirms_and_the_client_sees_the_closed_price()
    {
        var (scope, quotes, jobId) = await EstimateAsync(Flat, Occupancy.Furnished);
        using var _ = scope;
        // The admin sees catalog names in the request language (Accept-Language).
        System.Globalization.CultureInfo.CurrentUICulture = new("pt");

        var (draft, errors) = await quotes.SaveDraftAsync(jobId, QuoteOrigin.AdminReview, Guid.CreateVersion7(),
            new DraftQuoteRequest(Occupancy.Furnished, Flat, [AntiDamp], null, null, null));

        Assert.Null(errors);
        Assert.Equal((2, true, 318468L, 134767L, 66809L, 25.8m), (draft!.Number, draft.IsDraft, draft.Breakdown.TotalCents,
            draft.Breakdown.PainterTotalCents, draft.Breakdown.MarginCents, draft.Breakdown.MarginPct));
        Assert.Equal(new QuoteExtraView("anti_damp_treatment", "Tratamento anti-humidade", 12195, 7500), Assert.Single(draft.Extras));
        Assert.Equal("el-1", draft.Elements[0].ClientKey);

        var confirmed = await quotes.ConfirmDraftAsync(jobId, QuoteOrigin.AdminReview, Guid.CreateVersion7(), "  Inclui o tratamento.  ", false);

        Assert.NotNull(confirmed);
        var validUntil = BusinessDays.Today(TimeProvider.System).AddDays(15);
        Assert.Equal(new ConfirmedQuote(draft.Id, 318468, 95540, validUntil), confirmed);
        var client = await quotes.GetClientAsync(confirmed.Id, "es");
        Assert.NotNull(client);
        Assert.Equal(
            [
                new ClientQuoteLine(ClientQuoteLineKind.Work, null, 6, 266.44m, 303468),
                new ClientQuoteLine(ClientQuoteLineKind.Extra, "Tratamiento anti-humedad", null, null, 15000),
            ],
            client.Lines);
        Assert.Equal((318468L, 95540L, 222928L, true, validUntil, "Inclui o tratamento."),
            (client.TotalCents, client.DepositCents, client.BalanceCents, client.Premium, client.ValidUntil, client.Note));
        Assert.Null(await quotes.GetDraftAsync(jobId));
    }

    [Fact]
    public async Task A2_7_TC14_manual_subtotal_needs_a_reason_and_recomputes_the_price()
    {
        var (scope, quotes, jobId) = await EstimateAsync(Flat, Occupancy.Furnished);
        using var _ = scope;

        var (_, missingReason) = await quotes.SaveDraftAsync(jobId, QuoteOrigin.AdminReview, Guid.CreateVersion7(),
            new DraftQuoteRequest(Occupancy.Furnished, Flat, [AntiDamp], null, 250000, null));
        var (draft, errors) = await quotes.SaveDraftAsync(jobId, QuoteOrigin.AdminReview, Guid.CreateVersion7(),
            new DraftQuoteRequest(Occupancy.Furnished, Flat, [AntiDamp], null, 250000, "Cliente habitual"));

        Assert.True(missingReason!.Errors.ContainsKey("adjustmentReason"));
        Assert.Null(errors);
        Assert.Equal((250000L, 307500L, 57892L, 23.2m, 92250L, 215250L), (draft!.Breakdown.SubtotalCents, draft.Breakdown.TotalCents,
            draft.Breakdown.MarginCents, draft.Breakdown.MarginPct, draft.Breakdown.DepositCents, draft.Breakdown.BalanceCents));
        Assert.Equal((250000L, "Cliente habitual"), (draft.ManualSubtotalCents!.Value, draft.AdjustmentReason));
    }

    [Fact]
    public async Task A2_8_margin_under_the_minimum_needs_acknowledgment()
    {
        // TC07: 19.5% margin, under the 20% minimum.
        QuoteElementRequest[] bathroom =
            [new(ElementKind.Room, null, "bathroom", null, 2.5m, 2m, 2.4m, [true, true, true, true], true, null, "anti_damp", true)];
        var (scope, quotes, jobId) = await EstimateAsync(bathroom, Occupancy.Empty);
        using var _ = scope;

        var refused = await quotes.ConfirmDraftAsync(jobId, QuoteOrigin.AdminReview, Guid.CreateVersion7(), null, acknowledgeLowMargin: false);
        var draft = await quotes.GetDraftAsync(jobId);
        var confirmed = await quotes.ConfirmDraftAsync(jobId, QuoteOrigin.AdminReview, Guid.CreateVersion7(), null, acknowledgeLowMargin: true);

        Assert.Null(refused);
        Assert.True(draft!.Breakdown.BelowMinMargin);
        Assert.Equal(35297, confirmed!.TotalCents);
    }

    [Fact]
    public async Task A2_unknown_extra_or_free_extra_without_prices_is_rejected()
    {
        var (scope, quotes, jobId) = await EstimateAsync(Flat, Occupancy.Furnished);
        using var _ = scope;

        var (_, unknown) = await quotes.SaveDraftAsync(jobId, QuoteOrigin.AdminReview, Guid.CreateVersion7(),
            new DraftQuoteRequest(Occupancy.Furnished, Flat, [new("gold_leaf", null, null, null)], null, null, null));
        var (_, noPrice) = await quotes.SaveDraftAsync(jobId, QuoteOrigin.AdminReview, Guid.CreateVersion7(),
            new DraftQuoteRequest(Occupancy.Furnished, Flat, [new(null, "Rodapés", null, null)], null, null, null));
        var (free, ok) = await quotes.SaveDraftAsync(jobId, QuoteOrigin.AdminReview, Guid.CreateVersion7(),
            new DraftQuoteRequest(Occupancy.Furnished, Flat, [new(null, "Rodapés", 8000, 5000)], true, null, null));

        Assert.True(unknown!.Errors.ContainsKey("extras[0].catalogKey"));
        Assert.True(noPrice!.Errors.ContainsKey("extras[0].description"));
        Assert.Null(ok);
        Assert.Equal(new QuoteExtraView(null, "Rodapés", 8000, 5000), Assert.Single(free!.Extras));
        Assert.Equal((true, true), (free.PremiumOverride!.Value, free.Breakdown.Premium));
    }

    [Fact]
    public async Task A3_5_versions_keep_the_estimate_and_each_confirmed_price()
    {
        var (scope, quotes, jobId) = await EstimateAsync(Flat, Occupancy.Furnished);
        using var _ = scope;

        await quotes.ConfirmDraftAsync(jobId, QuoteOrigin.AdminReview, Guid.CreateVersion7(), null, false);
        var revision = await quotes.OpenDraftAsync(jobId, QuoteOrigin.Revision, Guid.CreateVersion7());
        var versions = await quotes.ListAsync(jobId);

        Assert.Equal(
            [(1, QuoteOrigin.Estimate, false, true), (2, QuoteOrigin.AdminReview, false, false), (3, QuoteOrigin.Revision, true, true)],
            versions.Select(v => (v.Number, v.Origin, v.IsDraft, v.ConfirmedAt is null)));
        Assert.Equal(revision.Id, (await quotes.GetDraftAsync(jobId))!.Id);
        Assert.Equal(versions[0].TotalCents, versions[2].TotalCents);
    }
}
