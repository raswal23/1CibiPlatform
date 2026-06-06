namespace FrontendWebassembly.Services.Shared.Implementation;

using FrontendWebassembly.Component.Generic;
using FrontendWebassembly.Services.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

public sealed class DialogWorkflowService : IDialogWorkflowService
{
	private static DialogOptions DefaultOptions => new()
	{
		CloseButton = false,
		MaxWidth = MaxWidth.Small,
		FullWidth = true
	};

	public async Task<TDto?> OpenAddDialogAsync<TComponent, TDto>(
		IDialogService dialogService,
		string title)
		where TComponent : ComponentBase
	{
		var dialog = await dialogService.ShowAsync<TComponent>(title, DefaultOptions);
		var result = await dialog.Result;

		return result?.Data is TDto dto ? dto : default;
	}

	public async Task<TDto?> OpenEditDialogAsync<TComponent, TDto>(
		IDialogService dialogService,
		string title,
		string parameterName,
		TDto dto)
		where TComponent : ComponentBase
	{
		var parameters = new DialogParameters
		{
			{ parameterName, dto }
		};

		var dialog = await dialogService.ShowAsync<TComponent>(title, parameters, DefaultOptions);
		var result = await dialog.Result;

		return result?.Data is TDto editedDto ? editedDto : default;
	}

	public async Task<bool> ConfirmActionAsync(
		IDialogService dialogService,
		string title,
		string contentText,
		string buttonText)
	{
		var parameters = new DialogParameters
		{
			{ "ContentText", contentText },
			{ "ButtonText", buttonText }
		};

		var dialog = await dialogService.ShowAsync<DeleteDialogConfirmation>(title, parameters, DefaultOptions);
		var result = await dialog.Result;

		return result is not null && !result.Canceled;
	}
}
