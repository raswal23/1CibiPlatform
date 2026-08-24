namespace FrontendWebassembly.DTO.SharedDTO;

// Mirror of BuildingBlocks.Pagination.KeysetPaginatedResult<TEntity>.
public class KeysetPaginatedResult<TEntity>
	(IReadOnlyList<TEntity> items, string? nextCursor, long? totalCount)
	where TEntity : class
{
	public IReadOnlyList<TEntity> Items { get; } = items;

	// null => last page.
	public string? NextCursor { get; } = nextCursor;

	// Populated only on the first page; cursor pages return null and the loader
	// reuses the count captured on page one.
	public long? TotalCount { get; } = totalCount;
}
