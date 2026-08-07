namespace FrontendWebassembly.Component.Generic;

public partial class YesNoDialogComponent
{
	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;

	[Parameter]
	public string Title { get; set; } = string.Empty;

	[Parameter]
	public string Message { get; set; } = string.Empty;

	[Parameter]
	public string ConfirmText { get; set; } = string.Empty;

	[Parameter]
	public string? InformationMessage { get; set; } = string.Empty;

	[Parameter]
	public string? AvatarIcon { get; set; } = Icons.Material.Filled.HelpOutline;

	[Parameter]
	public Color AvatarColor { get; set; } = Color.Primary;

	[Parameter]
	public string? InfoBGColor { get; set; } = "#EEF4FF";

	[Parameter]
	public Color InfoColor { get; set; } = Color.Primary;

	[Parameter]
	public string ThemeButtonColor { get; set; } = "theme-button-active";

	private void Confirm()
	{
		MudDialog.Close(DialogResult.Ok(true));
	}

	private void Cancel()
	{
		MudDialog.Cancel();
	}
}
