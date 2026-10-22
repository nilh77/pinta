namespace Pinta.Pricing;

/// <summary>A job to price: its elements, the occupancy key and, after review, the extra items.</summary>
public sealed record QuoteInput(IReadOnlyList<QuoteElement> Elements, string Occupancy, IReadOnlyList<ExtraLine>? Extras = null)
{
    public IReadOnlyList<ExtraLine> Extras { get; init; } = Extras ?? [];
}

/// <summary>A room or a loose surface.</summary>
public abstract record QuoteElement(string Paint, bool HasDamage);

/// <summary>
/// A room with walls A–D and ceiling. Either <see cref="Size"/> or <see cref="Length"/> and <see cref="Width"/>;
/// walls A and C measure the length, B and D the width. Height defaults to the snapshot's default height.
/// </summary>
public sealed record Room(
    string RoomType,
    RoomSize? Size,
    decimal? Length,
    decimal? Width,
    decimal? Height,
    Walls Walls,
    bool Ceiling,
    string Paint,
    bool HasDamage) : QuoteElement(Paint, HasDamage);

/// <summary>A loose surface of length × width; on a wall the width is the height.</summary>
public sealed record LooseSurface(string SurfaceType, decimal Length, decimal Width, string Paint, bool HasDamage)
    : QuoteElement(Paint, HasDamage);

public sealed record Walls(bool A, bool B, bool C, bool D)
{
    public bool Any => A || B || C || D;
}

public enum RoomSize
{
    S,
    M,
    L,
}

/// <summary>An extra item added by the admin: client price and painter pay, before VAT.</summary>
public sealed record ExtraLine(decimal ClientPrice, decimal PainterPay);
