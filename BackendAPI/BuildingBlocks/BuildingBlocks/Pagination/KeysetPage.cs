namespace BuildingBlocks.Pagination;

public static class KeysetPage
{
	public const int MaxPageSize = 100;

	public static int Clamp(int pageSize) => Math.Clamp(pageSize, 1, MaxPageSize);

	// items must have been fetched with Take(pageSize + 1); the extra row only signals
	// that a next page exists and is trimmed here.
	public static (List<T> Items, bool HasMore) Trim<T>(List<T> items, int pageSize)
	{
		var hasMore = items.Count > pageSize;
		if (hasMore)
		{
			items.RemoveAt(items.Count - 1);
		}

		return (items, hasMore);
	}
}
