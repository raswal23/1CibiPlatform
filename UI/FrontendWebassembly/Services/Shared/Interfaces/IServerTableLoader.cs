namespace FrontendWebassembly.Services.Shared.Interfaces;

public interface IServerTableLoader
{
	Task<TableData<TItem>> LoadPagedDataAsync<TItem>(
		TableState state,
		Func<int, int, Task<PaginatedResult<TItem>>> fetchData)
		where TItem : class;
}
