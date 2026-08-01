namespace AppCore.Contracts.Common.Pagination;

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    long TotalCount)
{
    public long TotalPages =>
        PageSize == 0
            ? 0
            : TotalCount / PageSize
              + (TotalCount % PageSize == 0 ? 0 : 1);
}
