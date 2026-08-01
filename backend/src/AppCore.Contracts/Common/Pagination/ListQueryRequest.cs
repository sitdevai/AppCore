using System.ComponentModel.DataAnnotations;

namespace AppCore.Contracts.Common.Pagination;

public sealed record ListQueryRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "validation.range")]
    public int PageNumber { get; init; } = 1;

    [Range(1, 100, ErrorMessage = "validation.range")]
    public int PageSize { get; init; } = 20;

    [StringLength(200, ErrorMessage = "validation.maxLength")]
    public string? Search { get; init; }

    [StringLength(100, ErrorMessage = "validation.maxLength")]
    public string? SortBy { get; init; }

    [EnumDataType(
        typeof(SortDirection),
        ErrorMessage = "validation.enum")]
    public SortDirection SortDirection { get; init; } = SortDirection.Ascending;
}
