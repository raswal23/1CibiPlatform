namespace FrontendWebassembly.Services.Shared.Implementation;

using FrontendWebassembly.Services.Shared.Interfaces;

// Adapts MudTable server-side loading to keyset (cursor) pagination.
// Stateful: instantiate one per table (not via DI). Navigation is strictly
// First/Prev/Next, so every reachable page's cursor is already recorded in the
// stack by the time the user can request it; there is no backend prev-cursor.
public sealed class CursorTableLoader<TItem> : ICursorPagerState
	where TItem : class
{
	// _cursors[i] is the cursor that fetches page i (0-based); page 0 is always null.
	private readonly List<string?> _cursors = [null];
	private long _totalCount;
	private string? _signature;

	public bool HasNextPage { get; private set; }
	public long TotalCount => _totalCount;
	public int ItemsOnPage { get; private set; }
	public int CurrentPage { get; private set; }

	// signature must include every input that invalidates the walk
	// (search term, sort column + direction, date range, page size).
	public async Task<TableData<TItem>> LoadAsync(
		TableState state,
		string signature,
		Func<string?, int, Task<ServiceResponse<KeysetPaginatedResult<TItem>>>> fetchData,
		Action<string>? onError = null)
	{
		var page = state.Page;

		if (signature != _signature)
		{
			_signature = signature;
			ResetCursors();
			page = 0;
		}

		// Defensive: only pages whose cursor is stacked are reachable.
		if (page >= _cursors.Count)
			page = _cursors.Count - 1;

		var response = await fetchData(_cursors[page], state.PageSize);

		if (!response.IsSuccess || response.Data is null)
		{
			onError?.Invoke(response.ErrorDetail);

			// A rejected cursor (stale after deploy, malformed) => restart the walk.
			Reset();

			return new TableData<TItem>
			{
				TotalItems = 0,
				Items = Array.Empty<TItem>()
			};
		}

		var result = response.Data;

		if (result.TotalCount.HasValue)
			_totalCount = result.TotalCount.Value;

		if (result.NextCursor is not null)
		{
			if (_cursors.Count == page + 1)
				_cursors.Add(result.NextCursor);
			else
				_cursors[page + 1] = result.NextCursor;
		}
		else if (_cursors.Count > page + 1)
		{
			// The walk ends here now; forget cursors past the last page.
			_cursors.RemoveRange(page + 1, _cursors.Count - page - 1);
		}

		HasNextPage = result.NextCursor is not null;
		CurrentPage = page;
		ItemsOnPage = result.Items.Count;

		return new TableData<TItem>
		{
			TotalItems = (int)_totalCount,
			Items = result.Items
		};
	}

	public void Reset()
	{
		ResetCursors();
		_signature = null;
	}

	private void ResetCursors()
	{
		_cursors.Clear();
		_cursors.Add(null);
	}
}
