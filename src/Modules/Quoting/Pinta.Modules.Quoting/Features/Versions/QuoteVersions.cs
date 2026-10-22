using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Catalog.Contracts;
using Pinta.Modules.Quoting.Contracts;
using Pinta.Modules.Quoting.Data;
using Pinta.Modules.Quoting.Domain;
using Pinta.Modules.Quoting.Features.Estimate;
using Pinta.Modules.Quoting.Features.Pricing;
using Pinta.Modules.Quoting.Resources;
using Pinta.Pricing;
using Pinta.SharedKernel;
using Pinta.SharedKernel.Http;
using Pinta.SharedKernel.Time;
using StoredElement = Pinta.Modules.Quoting.Domain.QuoteElement;

namespace Pinta.Modules.Quoting.Features.Versions;

internal sealed class QuoteVersions(
    QuotingDbContext db, IPriceSnapshotProvider snapshots, IExtraItems extraItems, IRequestSettings settings, TimeProvider clock)
    : IQuoteVersions
{
    public async Task<(QuoteVersionSummary? Version, QuoteErrors? Errors)> CreateEstimateAsync(
        Guid jobId, Guid createdBy, Occupancy occupancy, IReadOnlyList<QuoteElementRequest> elements, CancellationToken cancellationToken = default)
    {
        // Same checks as POST /api/quotes/estimate: request shape first, then the pricing rules.
        var shape = await new EstimateRequestValidator().ValidateAsync(new EstimateRequest(occupancy, elements), cancellationToken);
        if (!shape.IsValid)
        {
            return (null, new QuoteErrors(ValidationFilter<EstimateRequest>.ToErrors(shape)));
        }

        var priced = QuotePricing.PriceWith(await snapshots.GetCurrentAsync(cancellationToken), occupancy, elements, []);
        if (priced.Errors is not null)
        {
            return (null, new QuoteErrors(priced.Errors));
        }

        var version = new QuoteVersion
        {
            JobId = jobId,
            Number = 1,
            Origin = QuoteOrigin.Estimate,
            PriceSnapshot = priced.Snapshot,
            Occupancy = priced.Input.Occupancy,
            CreatedBy = createdBy,
            CreatedAt = clock.GetUtcNow(),
        };
        Apply(version, priced.Breakdown!, elements, []);
        db.Versions.Add(version);
        await db.SaveChangesAsync(cancellationToken);
        return (ToSummary(version), null);
    }

    public async Task<QuoteVersionSummary?> GetAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        var version = await Load().AsNoTracking().SingleOrDefaultAsync(v => v.Id == versionId, cancellationToken);
        return version is null ? null : ToSummary(version);
    }

    public async Task<AdminQuote?> GetAdminAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        var version = await Load().AsNoTracking().SingleOrDefaultAsync(v => v.Id == versionId, cancellationToken);
        return version is null ? null : ToAdmin(version);
    }

    public async Task<AdminQuote?> GetDraftAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var draft = await FindDraftAsync(jobId, tracked: false, cancellationToken);
        return draft is null ? null : ToAdmin(draft);
    }

    public async Task<AdminQuote> OpenDraftAsync(Guid jobId, QuoteOrigin origin, Guid createdBy, CancellationToken cancellationToken = default) =>
        ToAdmin(await OpenAsync(jobId, origin, createdBy, cancellationToken));

    public async Task<(AdminQuote? Quote, QuoteErrors? Errors)> SaveDraftAsync(
        Guid jobId, QuoteOrigin origin, Guid userId, DraftQuoteRequest request, CancellationToken cancellationToken = default)
    {
        var shape = await new DraftQuoteRequestValidator().ValidateAsync(request, cancellationToken);
        if (!shape.IsValid)
        {
            return (null, new QuoteErrors(ValidationFilter<DraftQuoteRequest>.ToErrors(shape)));
        }

        var (extras, extraErrors) = await ResolveExtrasAsync(request.Extras ?? [], cancellationToken);
        if (extraErrors.Count > 0)
        {
            return (null, new QuoteErrors(extraErrors));
        }

        var draft = await OpenAsync(jobId, origin, userId, cancellationToken);
        var manualSubtotal = request.ManualSubtotalCents is { } cents ? Money.FromCents(cents) : (decimal?)null;
        var priced = QuotePricing.PriceWith(
            draft.PriceSnapshot, request.Occupancy, request.Elements, [.. extras.Select(x => new ExtraLine(x.ClientPrice, x.PainterPay))], manualSubtotal);
        if (priced.Errors is not null)
        {
            return (null, new QuoteErrors(priced.Errors));
        }

        // Elements and extras are replaced as a whole: the draft is what the admin sees in the editor.
        db.RemoveRange(draft.Elements);
        db.RemoveRange(draft.Extras);
        draft.Elements.Clear();
        draft.Extras.Clear();
        draft.Occupancy = priced.Input.Occupancy;
        draft.PremiumOverride = request.Premium;
        draft.ManualAdjustment = manualSubtotal;
        draft.AdjustmentReason = manualSubtotal is null ? null : request.AdjustmentReason?.Trim();
        Apply(draft, priced.Breakdown!, request.Elements, extras);
        await db.SaveChangesAsync(cancellationToken);
        return (ToAdmin(draft), null);
    }

    public async Task<ConfirmedQuote?> ConfirmDraftAsync(
        Guid jobId, QuoteOrigin origin, Guid userId, string? note, bool acknowledgeLowMargin, CancellationToken cancellationToken = default)
    {
        var draft = await OpenAsync(jobId, origin, userId, cancellationToken);
        var breakdown = Calculate(draft);
        if (breakdown.BelowMinMargin && !acknowledgeLowMargin)
        {
            return null;
        }

        var validityDays = (await settings.GetAsync(cancellationToken)).QuoteValidityDays;
        draft.ConfirmedAt = clock.GetUtcNow();
        draft.ConfirmedBy = userId;
        draft.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        draft.ValidUntil = BusinessDays.Today(clock).AddDays(validityDays);
        await db.SaveChangesAsync(cancellationToken);
        return new ConfirmedQuote(draft.Id, Money.ToCents(breakdown.Total), Money.ToCents(breakdown.Deposit), draft.ValidUntil.Value);
    }

    public async Task<ClientQuote?> GetClientAsync(Guid versionId, string language, CancellationToken cancellationToken = default)
    {
        var version = await Load().AsNoTracking().SingleOrDefaultAsync(v => v.Id == versionId, cancellationToken);
        if (version is null)
        {
            return null;
        }

        var b = Calculate(version);
        var vatFactor = 1 + version.PriceSnapshot.Rules.VatPct / 100m;
        // Each extra with its VAT; the work line takes the rest, so the lines always add up to the total.
        var extras = version.Extras
            .Select(x => new ClientQuoteLine(ClientQuoteLineKind.Extra, x.Name?.In(language) ?? x.Description, null, null,
                Money.ToCents(Math.Round(x.ClientPrice * vatFactor, 2, MidpointRounding.AwayFromZero))))
            .ToList();
        var total = Money.ToCents(b.Total);
        var work = new ClientQuoteLine(ClientQuoteLineKind.Work, null, version.Elements.Count, b.AreaM2, total - extras.Sum(x => x.AmountCents));
        return new ClientQuote(
            version.Id, version.Number, [work, .. extras], b.AreaM2, total, Money.ToCents(b.Vat), Money.ToCents(b.Deposit),
            Money.ToCents(b.Balance), version.Premium, version.ValidUntil, version.Note, version.ConfirmedAt);
    }

    public async Task<IReadOnlyList<QuoteVersionListItem>> ListAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        (await db.Versions.AsNoTracking().Where(v => v.JobId == jobId).OrderBy(v => v.Number).ToListAsync(cancellationToken))
        .Select(v => new QuoteVersionListItem(
            v.Id, v.Number, v.Origin, v.IsDraft, v.AreaM2, Money.ToCents(v.Total), v.ManualAdjustment is not null, v.CreatedAt,
            v.ConfirmedAt, v.ValidUntil))
        .ToList();

    private IQueryable<QuoteVersion> Load() =>
        db.Versions.Include(v => v.Elements.OrderBy(e => e.Position)).Include(v => v.Extras).AsSplitQuery();

    private Task<QuoteVersion?> FindDraftAsync(Guid jobId, bool tracked, CancellationToken cancellationToken)
    {
        var query = Load().Where(v => v.JobId == jobId && v.ConfirmedAt == null && v.Origin != QuoteOrigin.Estimate);
        return (tracked ? query : query.AsNoTracking()).OrderByDescending(v => v.Number).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>The open draft, or a copy of the latest version priced with the current configuration.</summary>
    private async Task<QuoteVersion> OpenAsync(Guid jobId, QuoteOrigin origin, Guid createdBy, CancellationToken cancellationToken)
    {
        var draft = await FindDraftAsync(jobId, tracked: true, cancellationToken);
        if (draft is not null)
        {
            return draft;
        }

        var latest = await Load().AsNoTracking().Where(v => v.JobId == jobId).OrderByDescending(v => v.Number).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Job {jobId} has no quote version.");
        var elements = latest.Elements.Select(ToRequest).ToList();
        var extras = latest.Extras.Select(x => new ResolvedExtra(x.CatalogKey, x.Name, x.Description, x.ClientPrice, x.PainterPay)).ToList();
        var priced = QuotePricing.PriceWith(
            await snapshots.GetCurrentAsync(cancellationToken), QuotePricing.ParseOccupancy(latest.Occupancy), elements,
            [.. extras.Select(x => new ExtraLine(x.ClientPrice, x.PainterPay))], latest.ManualAdjustment);
        if (priced.Errors is not null)
        {
            // An item deactivated since: the admin fixes it in the editor, so the copy keeps the old configuration.
            priced = QuotePricing.PriceWith(latest.PriceSnapshot, QuotePricing.ParseOccupancy(latest.Occupancy), elements,
                [.. extras.Select(x => new ExtraLine(x.ClientPrice, x.PainterPay))], latest.ManualAdjustment);
        }

        draft = new QuoteVersion
        {
            JobId = jobId,
            Number = latest.Number + 1,
            Origin = origin,
            PriceSnapshot = priced.Snapshot,
            Occupancy = latest.Occupancy,
            PremiumOverride = latest.PremiumOverride,
            ManualAdjustment = latest.ManualAdjustment,
            AdjustmentReason = latest.AdjustmentReason,
            CreatedBy = createdBy,
            CreatedAt = clock.GetUtcNow(),
        };
        Apply(draft, priced.Breakdown!, elements, extras);
        db.Versions.Add(draft);
        await db.SaveChangesAsync(cancellationToken);
        return draft;
    }

    private sealed record ResolvedExtra(string? CatalogKey, Pinta.SharedKernel.Text.LocalizedText? Name, string Description, decimal ClientPrice, decimal PainterPay);

    private async Task<(List<ResolvedExtra> Extras, Dictionary<string, string[]> Errors)> ResolveExtrasAsync(
        IReadOnlyList<DraftExtraRequest> requests, CancellationToken cancellationToken)
    {
        var extras = new List<ResolvedExtra>();
        var errors = new Dictionary<string, string[]>();
        for (var i = 0; i < requests.Count; i++)
        {
            var r = requests[i];
            if (string.IsNullOrWhiteSpace(r.CatalogKey))
            {
                extras.Add(new ResolvedExtra(null, null, r.Description!.Trim(), Money.FromCents(r.ClientPriceCents!.Value), Money.FromCents(r.PainterPayCents!.Value)));
                continue;
            }

            var item = await extraItems.FindActiveAsync(r.CatalogKey, cancellationToken);
            if (item is null)
            {
                errors[$"extras[{i}].catalogKey"] = [QuotingMessages.Get("unknown_extra")];
                continue;
            }

            extras.Add(new ResolvedExtra(
                item.Key,
                item.Name,
                string.IsNullOrWhiteSpace(r.Description) ? item.Name.Pt : r.Description.Trim(),
                r.ClientPriceCents is { } client ? Money.FromCents(client) : item.ClientPrice,
                r.PainterPayCents is { } painter ? Money.FromCents(painter) : item.PainterPay));
        }

        return (extras, errors);
    }

    /// <summary>Stores the priced totals, elements and extras on the version.</summary>
    private static void Apply(QuoteVersion version, QuoteBreakdown b, IReadOnlyList<QuoteElementRequest> elements, IReadOnlyList<ResolvedExtra> extras)
    {
        version.AreaM2 = b.AreaM2;
        version.MaterialCost = b.MaterialCost;
        version.LabourPrice = b.LabourPrice;
        version.Subtotal = b.Subtotal;
        version.Vat = b.Vat;
        version.Total = b.Total;
        version.PainterPay = b.PainterPay;
        version.Margin = b.Margin;
        version.RangeMin = b.Range?.Min;
        version.RangeMax = b.Range?.Max;
        version.MinimumApplied = b.MinimumApplied;
        version.OverMax = b.OverMax;
        version.Premium = version.PremiumOverride ?? b.Premium;
        for (var i = 0; i < elements.Count; i++)
        {
            var e = elements[i];
            version.Elements.Add(new StoredElement
            {
                QuoteVersionId = version.Id,
                Position = i,
                ClientKey = e.ClientKey,
                Name = e.Name?.Trim(),
                Kind = e.Kind == ElementKind.Room ? "room" : "surface",
                RoomType = e.Kind == ElementKind.Room ? e.RoomTypeId : null,
                SurfaceType = e.Kind == ElementKind.Surface ? e.SurfaceTypeId : null,
                Size = e.Size,
                Length = e.Length,
                Width = e.Width,
                Height = e.Height,
                WallA = e.Walls?[0] ?? false,
                WallB = e.Walls?[1] ?? false,
                WallC = e.Walls?[2] ?? false,
                WallD = e.Walls?[3] ?? false,
                Ceiling = e.Kind == ElementKind.Room && e.Ceiling,
                Paint = e.PaintId,
                HasDamage = e.Damage,
                AreaM2 = b.Elements[i].AreaM2,
            });
        }

        foreach (var x in extras)
        {
            version.Extras.Add(new QuoteExtra
            {
                QuoteVersionId = version.Id,
                CatalogKey = x.CatalogKey,
                Name = x.Name,
                Description = x.Description,
                ClientPrice = x.ClientPrice,
                PainterPay = x.PainterPay,
            });
        }
    }

    /// <summary>The engine is pure: the stored snapshot and contents give back the same breakdown every time.</summary>
    private static QuoteBreakdown Calculate(QuoteVersion v) =>
        PricingEngine.Calculate(v.PriceSnapshot, new QuoteInput(
            [.. v.Elements.OrderBy(e => e.Position).Select(ToRequest).Select(QuotePricing.ToElement)],
            v.Occupancy,
            [.. v.Extras.Select(x => new ExtraLine(x.ClientPrice, x.PainterPay))]) { ManualSubtotal = v.ManualAdjustment });

    private static QuoteElementRequest ToRequest(StoredElement e) => new(
        e.Kind == "room" ? ElementKind.Room : ElementKind.Surface,
        e.Name,
        e.RoomType,
        e.Size,
        e.Length,
        e.Width,
        e.Height,
        e.Kind == "room" ? [e.WallA, e.WallB, e.WallC, e.WallD] : null,
        e.Ceiling,
        e.SurfaceType,
        e.Paint,
        e.HasDamage,
        e.ClientKey);

    private static AdminQuote ToAdmin(QuoteVersion v)
    {
        var b = Calculate(v);
        var language = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var extras = v.Extras.Select(x => new QuoteExtraView(x.CatalogKey, x.Name?.In(language) ?? x.Description, Money.ToCents(x.ClientPrice), Money.ToCents(x.PainterPay))).ToList();
        var breakdown = BreakdownMapper.ToDto(b, [.. extras.Select(x => new ExtraLineDto(x.Description, x.ClientPriceCents, x.PainterPayCents))]) with
        {
            Premium = v.Premium,
        };
        return new AdminQuote(
            v.Id,
            v.Number,
            v.Origin,
            v.IsDraft,
            QuotePricing.ParseOccupancy(v.Occupancy),
            v.Elements.OrderBy(e => e.Position).Select(e =>
            {
                var r = ToRequest(e);
                return new QuoteElementView(e.Id, r.Kind, r.Name, r.RoomTypeId, r.Size, r.Length, r.Width, r.Height, r.Walls, r.Ceiling,
                    r.SurfaceTypeId, r.PaintId, r.Damage, r.ClientKey, e.AreaM2);
            }).ToList(),
            extras,
            v.PremiumOverride,
            v.ManualAdjustment is { } manual ? Money.ToCents(manual) : null,
            v.AdjustmentReason,
            v.Note,
            v.ValidUntil,
            v.CreatedAt,
            v.ConfirmedAt,
            breakdown);
    }

    private static QuoteVersionSummary ToSummary(QuoteVersion v) => new(
        v.Id,
        v.Number,
        v.AreaM2,
        Money.ToCents(v.Total),
        v.RangeMin is null ? null : Money.ToCents(v.RangeMin.Value),
        v.RangeMax is null ? null : Money.ToCents(v.RangeMax.Value),
        v.MinimumApplied,
        v.OverMax,
        v.Premium,
        v.Elements.Where(e => e.ClientKey is not null).GroupBy(e => e.ClientKey!).ToDictionary(g => g.Key, g => g.First().Id));
}
