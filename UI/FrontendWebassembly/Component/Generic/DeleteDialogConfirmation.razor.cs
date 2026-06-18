using Microsoft.AspNetCore.Components;

namespace FrontendWebassembly.Component.Generic;
public partial class DeleteDialogConfirmation
{
	[CascadingParameter]
	IMudDialogInstance MudDialog { get; set; }

	[Parameter]
	public string ContentText { get; set; }
	[Parameter]
	public string ButtonText { get; set; }

	void Cancel() => MudDialog.Cancel();
	void Submit() => MudDialog.Close(DialogResult.Ok(true));
}

