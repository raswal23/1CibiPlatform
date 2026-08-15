namespace FrontendWebassembly.Component.Special;

using FrontendWebassembly.Component.Generic;
using FrontendWebassembly.Services.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

public abstract class CrudPageBase : SecurePageBase
{
	[Inject] protected IDialogWorkflowService DialogWorkflowService { get; set; } = default!;
	[Inject] protected IServerTableLoader ServerTableLoader { get; set; } = default!;
	[Inject] protected IDialogService DialogService { get; set; } = default!;
	[Inject] protected ISnackbar Snackbar { get; set; } = default!;

	protected Task<TableData<TItem>> LoadPagedDataAsync<TItem>(
		TableState state,
		Func<int, int, Task<ServiceResponse<PaginatedResult<TItem>>>> fetchData)
		where TItem : class
		=> ServerTableLoader.LoadPagedDataAsync(state, fetchData,
			message => Snackbar.Add(message, Severity.Error));

	protected async Task OpenAddDialogAsync<TComponent, TDto>(
		string title,
		Func<TDto, Task> onAdd)
		where TComponent : ComponentBase
	{
		var dto = await DialogWorkflowService.OpenAddDialogAsync<TComponent, TDto>(DialogService, title);
		if (dto is not null)
		{
			await onAdd(dto);
		}
	}

	protected async Task OpenEditDialogAsync<TComponent, TDto>(
		string title,
		string parameterName,
		TDto dto,
		Func<TDto, Task> onEdit)
		where TComponent : ComponentBase
	{
		var editedDto = await DialogWorkflowService.OpenEditDialogAsync<TComponent, TDto>(
			DialogService,
			title,
			parameterName,
			dto);

		if (editedDto is not null)
		{
			await onEdit(editedDto);
		}
	}

	protected Task<bool> ConfirmActionAsync(string title, string contentText, string buttonText)
		=> DialogWorkflowService.ConfirmActionAsync(DialogService, title, contentText, buttonText);

	protected async Task<bool> ExecuteAndReloadAsync<TItem, TResult>(
		Func<Task<ServiceResponse<TResult>>> action,
		TableComponent<TItem> table,
		string? successMessage = null)
		where TItem : class
	{
		var response = await action();

		if (!response.IsSuccess)
		{
			Snackbar.Add(response.ErrorDetail, Severity.Error);
			return false;
		}

		if (!string.IsNullOrEmpty(successMessage))
		{
			Snackbar.Add(successMessage, Severity.Success);
		}

		await table.TableRef.ReloadServerData();
		return true;
	}
}
