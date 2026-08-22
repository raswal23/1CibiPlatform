namespace FrontendWebassembly.Component.Special;

using FrontendWebassembly.Component.Generic;
using FrontendWebassembly.Services.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

public abstract class CrudPageBase : SecurePageBase
{
	[Inject] protected IDialogWorkflowService DialogWorkflowService { get; set; } = default!;
	[Inject] protected IDialogService DialogService { get; set; } = default!;
	[Inject] protected ISnackbar Snackbar { get; set; } = default!;

	// signature must include every filter that invalidates the keyset walk
	// (search term, sort column + direction, date range, page size).
	protected Task<TableData<TItem>> LoadCursorPagedDataAsync<TItem>(
		CursorTableLoader<TItem> loader,
		TableState state,
		string signature,
		Func<string?, int, Task<ServiceResponse<KeysetPaginatedResult<TItem>>>> fetchData)
		where TItem : class
		=> loader.LoadAsync(state, signature, fetchData,
			message => Snackbar.Add(message, Severity.Error));

	protected async Task OpenAddDialogAsync<TComponent, TDto>(
		string title,
		Func<TDto, Task> onAdd,
		DialogOptions? options = null)
		where TComponent : ComponentBase
	{
		var dto = await DialogWorkflowService.OpenAddDialogAsync<TComponent, TDto>(DialogService, title, options);
		if (dto is not null)
		{
			await onAdd(dto);
		}
	}

	protected async Task OpenEditDialogAsync<TComponent, TDto>(
		string title,
		string parameterName,
		TDto dto,
		Func<TDto, Task> onEdit,
		DialogOptions? options = null)
		where TComponent : ComponentBase
	{
		var editedDto = await DialogWorkflowService.OpenEditDialogAsync<TComponent, TDto>(
			DialogService,
			title,
			parameterName,
			dto,
			options);

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
