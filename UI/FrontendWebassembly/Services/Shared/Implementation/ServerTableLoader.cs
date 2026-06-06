namespace FrontendWebassembly.Services.Shared.Implementation;

using FrontendWebassembly.Services.Shared.Interfaces;

public sealed class ServerTableLoader : IServerTableLoader
{
	public async Task<TableData<TItem>> LoadPagedDataAsync<TItem>(
		TableState state,
		Func<int, int, Task<PaginatedResult<TItem>>> fetchData)
		where TItem : class
	{
		var result = await fetchData(state.Page + 1, state.PageSize);
		return new TableData<TItem>
		{
			TotalItems = (int)result.Count,
			Items = result.Data
		};
	}
}
