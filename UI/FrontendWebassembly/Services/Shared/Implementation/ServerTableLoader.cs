namespace FrontendWebassembly.Services.Shared.Implementation;

using FrontendWebassembly.Services.Shared.Interfaces;

public sealed class ServerTableLoader : IServerTableLoader
{
	public async Task<TableData<TItem>> LoadPagedDataAsync<TItem>(
		TableState state,
		Func<int, int, Task<ServiceResponse<PaginatedResult<TItem>>>> fetchData,
		Action<string>? onError = null)
		where TItem : class
	{
		var response = await fetchData(state.Page + 1, state.PageSize);

		if (!response.IsSuccess || response.Data is null)
		{
			onError?.Invoke(response.ErrorDetail);

			return new TableData<TItem>
			{
				TotalItems = 0,
				Items = Array.Empty<TItem>()
			};
		}

		return new TableData<TItem>
		{
			TotalItems = (int)response.Data.Count,
			Items = response.Data.Data
		};
	}
}
