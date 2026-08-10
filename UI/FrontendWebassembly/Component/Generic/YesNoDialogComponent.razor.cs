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

	[Parameter]
	public string? ConfirmIcon { get; set; }

	[Parameter]
	public Func<Task<bool>>? ConfirmActionAsync { get; set; }

	private bool isConfirming;

	private string ToneCssClass =>
		AvatarColor == Color.Warning ||
		InfoColor == Color.Warning ||
		ThemeButtonColor.Contains("warning", StringComparison.OrdinalIgnoreCase)
			? "is-warning"
			: "is-primary";

	private string? InfoBannerStyle => string.IsNullOrWhiteSpace(InfoBGColor)
		? null
		: $"--yes-no-info-background: {InfoBGColor};";

	private RenderFragment RenderInformationMessage() => builder =>
	{
		var message = InformationMessage ?? string.Empty;
		var actionIndex = string.IsNullOrWhiteSpace(ConfirmText)
			? -1
			: message.IndexOf(ConfirmText, StringComparison.OrdinalIgnoreCase);

		if (actionIndex < 0)
		{
			builder.AddContent(0, message);
			return;
		}

		builder.AddContent(0, message[..actionIndex]);
		builder.OpenElement(1, "strong");
		builder.AddContent(2, message.Substring(actionIndex, ConfirmText.Length));
		builder.CloseElement();
		builder.AddContent(3, message[(actionIndex + ConfirmText.Length)..]);
	};

	private async Task Confirm()
	{
		if (isConfirming)
			return;

		if (ConfirmActionAsync is null)
		{
			MudDialog.Close(DialogResult.Ok(true));
			return;
		}

		isConfirming = true;

		try
		{
			if (await ConfirmActionAsync())
				MudDialog.Close(DialogResult.Ok(true));
		}
		finally
		{
			isConfirming = false;
		}
	}

	private void Cancel()
	{
		if (isConfirming)
			return;

		MudDialog.Cancel();
	}
}
