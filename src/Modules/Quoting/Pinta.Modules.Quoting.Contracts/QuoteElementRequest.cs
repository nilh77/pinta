using Pinta.Pricing;

namespace Pinta.Modules.Quoting.Contracts;

/// <summary>Serialized as <c>room</c>, <c>surface</c>.</summary>
public enum ElementKind
{
    Room,
    Surface,
}

/// <summary>Serialized as <c>empty</c>, <c>furnished</c>, <c>inhabited</c>; the key of an occupancy modifier.</summary>
public enum Occupancy
{
    Empty,
    Furnished,
    Inhabited,
}

/// <summary>
/// A room (<see cref="RoomTypeId"/>, <see cref="Size"/> or measures, <see cref="Walls"/> A–D, <see cref="Ceiling"/>) or a
/// loose surface (<see cref="SurfaceTypeId"/>, length × width). Catalog items are referenced by key (docs/api.md).
/// <see cref="ClientKey"/> is the builder's id for the element; photos uploaded for it carry the same key.
/// </summary>
public sealed record QuoteElementRequest(
    ElementKind Kind,
    string? Name,
    string? RoomTypeId,
    RoomSize? Size,
    decimal? Length,
    decimal? Width,
    decimal? Height,
    bool[]? Walls,
    bool Ceiling,
    string? SurfaceTypeId,
    string PaintId,
    bool Damage,
    string? ClientKey = null);
