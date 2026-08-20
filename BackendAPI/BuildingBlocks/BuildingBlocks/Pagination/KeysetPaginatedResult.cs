namespace BuildingBlocks.Pagination;
public class KeysetPaginatedResult<TEntity>
	(IReadOnlyList<TEntity> items, string? nextCursor, long? totalCount)
	where TEntity : class
{
	public IReadOnlyList<TEntity> Items { get; } = items;

	// null => last page.
	public string? NextCursor { get; } = nextCursor;

	// Populated only on the first page (request.Cursor == null); cursor pages return null
	// and callers reuse the count captured on page one.
	public long? TotalCount { get; } = totalCount;
}
