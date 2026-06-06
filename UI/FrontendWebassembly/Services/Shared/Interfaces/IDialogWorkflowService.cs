namespace FrontendWebassembly.Services.Shared.Interfaces;

using Microsoft.AspNetCore.Components;

public interface IDialogWorkflowService
{
	Task<TDto?> OpenAddDialogAsync<TComponent, TDto>(
		IDialogService dialogService,
		string title)
		where TComponent : ComponentBase;

	Task<TDto?> OpenEditDialogAsync<TComponent, TDto>(
		IDialogService dialogService,
		string title,
		string parameterName,
		TDto dto)
		where TComponent : ComponentBase;

	Task<bool> ConfirmActionAsync(
		IDialogService dialogService,
		string title,
		string contentText,
		string buttonText);
}
