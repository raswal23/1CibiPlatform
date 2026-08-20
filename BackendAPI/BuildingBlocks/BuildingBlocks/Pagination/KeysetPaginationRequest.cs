namespace BuildingBlocks.Pagination;
public record KeysetPaginationRequest(
	string? Cursor = null,
	int PageSize = 10,
	string? SearchTerm = null,
	DateTime? StartDate = null,
	DateTime? EndDate = null);
