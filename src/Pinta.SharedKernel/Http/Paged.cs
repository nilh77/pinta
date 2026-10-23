namespace Pinta.SharedKernel.Http;

/// <summary>A page of results (docs/api.md, "Paginación"): <c>?page=1&amp;pageSize=25</c>, at most 100 per page.</summary>
public sealed record Paged<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public const int MaxPageSize = 100;

    public static (int Page, int PageSize) Normalize(int? page, int? pageSize) =>
        (Math.Max(1, page ?? 1), Math.Clamp(pageSize ?? 25, 1, MaxPageSize));
}
