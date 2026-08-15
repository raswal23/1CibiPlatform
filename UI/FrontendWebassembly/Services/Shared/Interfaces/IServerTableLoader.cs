namespace FrontendWebassembly.Services.Shared.Interfaces;

public interface IServerTableLoader
{
	Task<TableData<TItem>> LoadPagedDataAsync<TItem>(
		TableState state,
		Func<int, int, Task<ServiceResponse<PaginatedResult<TItem>>>> fetchData,
		Action<string>? onError = null)
		where TItem : class;
}
