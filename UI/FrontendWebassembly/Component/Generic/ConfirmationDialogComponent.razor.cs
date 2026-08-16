namespace FrontendWebassembly.Component.Generic;

public partial class ConfirmationDialogComponent
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
	public Color ConfirmButtonColor { get; set; } = Color.Primary;

	[Parameter]
	public string? Footnote { get; set; }

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

	private void Confirm()
	{
		MudDialog.Close(DialogResult.Ok(true));
	}

	private void Cancel()
	{
		MudDialog.Cancel();
	}

}
