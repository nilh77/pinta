using Microsoft.EntityFrameworkCore;
using Pinta.Modules.Catalog.Data;
using Pinta.Modules.Catalog.Domain;
using Pinta.SharedKernel.Data;
using Pinta.SharedKernel.Text;

namespace Pinta.Api.Seed;

/// <summary>The seed configuration of docs/pricing-engine.md and the Lisbon district coverage (docs/plan.md, "Catálogo").</summary>
public static class CatalogSeeder
{
    // Main postal prefixes per concelho. Approximate and pending review before the demo (docs/plan.md).
    private static readonly (string Municipality, string[] Prefixes)[] Coverage =
    [
        ("Lisboa", ["1000", "1050", "1070", "1100", "1150", "1170", "1200", "1250", "1300", "1350", "1400", "1500", "1600", "1700", "1750", "1800", "1900", "1950", "1990", "1998"]),
        ("Alenquer", ["2580"]),
        ("Amadora", ["2610", "2650", "2700", "2720"]),
        ("Arruda dos Vinhos", ["2630"]),
        ("Azambuja", ["2050"]),
        ("Cadaval", ["2550"]),
        ("Cascais", ["2645", "2750", "2765", "2775", "2785"]),
        ("Loures", ["1885", "2660", "2670", "2680", "2685", "2690", "2695"]),
        ("Lourinhã", ["2530"]),
        ("Mafra", ["2640", "2655", "2665"]),
        ("Odivelas", ["1675", "1685", "2620", "2675"]),
        ("Oeiras", ["1495", "2730", "2740", "2760", "2770", "2780", "2790", "2795"]),
        ("Sintra", ["2605", "2635", "2705", "2710", "2714", "2715", "2725", "2735", "2745"]),
        ("Sobral de Monte Agraço", ["2590"]),
        ("Torres Vedras", ["2560", "2565"]),
        ("Vila Franca de Xira", ["2600", "2615", "2625"]),
    ];

    public static async Task SeedAsync(CatalogDbContext db, DateTimeOffset now)
    {
        if (await db.Settings.AnyAsync())
        {
            return;
        }

        var matte = Paint("matte", "Tinta plástica mate", "Plástica mate", "Matte emulsion", 8.00m, 10m);
        var satin = Paint("satin", "Tinta plástica acetinada", "Plástica satinada", "Satin emulsion", 11.00m, 10m);
        var antiDamp = Paint("anti_damp", "Tinta anti-humidade", "Anti-humedad", "Anti-damp paint", 16.00m, 8m);
        var exterior = Paint("exterior", "Tinta acrílica de exterior", "Exterior acrílica", "Acrylic exterior paint", 14.00m, 9m);
        Paint[] paints = [matte, satin, antiDamp, exterior];

        SurfaceType[] surfaces =
        [
            Surface("interior_wall", "Parede interior", "Pared interior", "Interior wall", 6.00m, 4.00m, 1.00m),
            Surface("ceiling", "Teto", "Techo", "Ceiling", 7.00m, 4.80m, 1.15m),
            Surface("facade", "Fachada", "Fachada", "Façade", 12.00m, 8.50m, 1.40m),
            Surface("wood", "Madeira", "Madera", "Wood", 9.00m, 6.00m, 1.20m),
            Surface("metal", "Metal", "Metal", "Metal", 10.00m, 7.00m, 1.30m),
        ];

        Modifier[] occupancies =
        [
            Occupancy("empty", "Vazia", "Vacía", "Empty", 1.00m),
            Occupancy("furnished", "Com móveis", "Con muebles", "Furnished", 1.10m),
            Occupancy("inhabited", "Habitada", "Habitada", "Lived in", 1.20m),
        ];

        RoomType[] rooms =
        [
            Room("living", "Sala", "Sala", "Living room", matte, (4m, 3m), (5m, 4m), (6m, 5m)),
            Room("bedroom", "Quarto", "Dormitorio", "Bedroom", matte, (3m, 2.5m), (3.5m, 3m), (4.5m, 4m)),
            Room("kitchen", "Cozinha", "Cocina", "Kitchen", satin, (3m, 2m), (3.5m, 3m), (4.5m, 3.5m)),
            Room("bathroom", "Casa de banho", "Baño", "Bathroom", antiDamp, (2m, 1.5m), (2.5m, 2m), (3m, 2.5m)),
            Room("hallway", "Corredor", "Pasillo", "Hallway", matte, (3m, 1m), (5m, 1.2m), (7m, 1.2m)),
            Room("other", "Outra divisão", "Otra", "Other", matte, (2.5m, 2.5m), (3m, 3m), (4m, 3.5m)),
        ];

        ExtraItem[] extras =
        [
            new ExtraItem
            {
                Key = "anti_damp_treatment",
                Name = Text("Tratamento anti-humidade", "Tratamiento anti-humedad", "Anti-damp treatment"),
                ClientPrice = 121.95m,
                PainterPay = 75.00m,
            },
        ];

        var settings = new PricingSettings
        {
            WastePct = 10m,
            VatPct = 23m,
            MinJobAmount = 150.00m,
            MaxJobAmount = 15_000.00m,
            PremiumAreaM2 = 250m,
            RangeLowPct = 10m,
            RangeHighPct = 15m,
            RangeSizeWideningPct = 10m,
            RangeDamageWideningPct = 10m,
            DepositPct = 30m,
            MinMarginPct = 20m,
            DefaultHeight = 2.60m,
            QuoteValidityDays = 15,
            MinLeadBusinessDays = 3,
            SiteVisitPrice = 0.00m,
            SiteVisitDeductible = true,
            ReviewDeadlineBusinessDays = 2,
            FullRefundMinDays = 7,
            LateRefundPct = 50m,
            MaxPhotosPerElement = 10,
            MaxVideosPerElement = 2,
            MaxImageSizeMb = 10,
            MaxVideoSizeMb = 100,
            ClientPhoneVisibleToPainter = true,
            PainterPhoneVisibleToClient = true,
        };

        var coverage = Coverage.SelectMany(c => c.Prefixes.Select(prefix =>
            new CoverageArea { PostalPrefix = prefix, Municipality = c.Municipality })).ToArray();

        // Lists are ordered by creation time: one millisecond apart keeps the order of docs/pricing-engine.md.
        AuditedEntity[] all = [.. paints, .. surfaces, .. occupancies, .. rooms, .. extras, settings, .. coverage];
        for (var i = 0; i < all.Length; i++)
        {
            all[i].Stamp(now.AddMilliseconds(i), userId: null);
        }

        db.AddRange(all);
        await db.SaveChangesAsync();
    }

    private static LocalizedText Text(string pt, string es, string en) => new() { Pt = pt, Es = es, En = en };

    private static Paint Paint(string key, string pt, string es, string en, decimal pricePerLitre, decimal coverage) => new()
    {
        Key = key,
        Name = Text(pt, es, en),
        PricePerLitre = pricePerLitre,
        CoverageM2PerLitre = coverage,
        Coats = 2,
    };

    private static SurfaceType Surface(string key, string pt, string es, string en, decimal client, decimal painter, decimal factor) => new()
    {
        Key = key,
        Name = Text(pt, es, en),
        ClientRatePerM2 = client,
        PainterRatePerM2 = painter,
        DifficultyFactor = factor,
    };

    private static Modifier Occupancy(string key, string pt, string es, string en, decimal factor) => new()
    {
        Key = key,
        Name = Text(pt, es, en),
        Factor = factor,
    };

    private static RoomType Room(string key, string pt, string es, string en, Paint suggested,
        (decimal L, decimal W) s, (decimal L, decimal W) m, (decimal L, decimal W) l) => new()
    {
        Key = key,
        Name = Text(pt, es, en),
        SuggestedPaintId = suggested.Id,
        Sizes = new RoomSizes
        {
            S = new RoomMeasures { Length = s.L, Width = s.W },
            M = new RoomMeasures { Length = m.L, Width = m.W },
            L = new RoomMeasures { Length = l.L, Width = l.W },
        },
    };
}
