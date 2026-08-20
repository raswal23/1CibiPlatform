namespace FrontendWebassembly.Services.Shared.Interfaces;

// Read-only view of a CursorTableLoader<TItem> consumed by CursorTablePager,
// which is non-generic and must not depend on the row type.
public interface ICursorPagerState
{
	bool HasNextPage { get; }
	long TotalCount { get; }
	int ItemsOnPage { get; }
}
