namespace FrontendWebassembly.Component.ATS;

public partial class ATSResultComponent
{
	private MudForm? form;
	private bool IsLoaded = true;

	private string GetOrderStatusText() => OrderStatusDisplay.GetText(ReportResult?.OrderStatus);
	private string GetOrderStatusClass() => OrderStatusDisplay.GetClass(ReportResult?.OrderStatus);

	private static string GetDocumentMeta(string? fileName, string? uploadedAt)
		=> string.IsNullOrWhiteSpace(fileName)
			? "Not yet uploaded"
			: $"{fileName} · uploaded {uploadedAt}";

	private static string GetDocumentMetaClass(string? fileName)
		=> string.IsNullOrWhiteSpace(fileName)
			? "ats-result-doc-meta-wrap pending"
			: "ats-result-doc-meta-wrap has-tooltip";

	private static string? GetDocumentTooltip(string? fileName, string? uploadedAt)
		=> string.IsNullOrWhiteSpace(fileName)
			? null
			: GetDocumentMeta(fileName, uploadedAt);

	private static int? GetDocumentTabIndex(string? fileName)
		=> string.IsNullOrWhiteSpace(fileName) ? null : 0;

	private string GetResultText()
		=> HitStatusDisplay.GetClass(ReportResult?.HitStatus) == "pending"
			? "Not yet available"
			: HitStatusDisplay.GetText(ReportResult?.HitStatus);

	private string GetResultValueClass()
		=> HitStatusDisplay.GetClass(ReportResult?.HitStatus) == "pending" ? "muted" : string.Empty;

	[Parameter]
	public Guid EmailInvitationId { get; set; }

	[Parameter]
	public ATSResultDetailsDTO? ReportResult { get; set; }

	[CascadingParameter]
	private IMudDialogInstance MudDialog { get; set; } = default!;

	private async Task OpenResultDialog<TComponent>(
	string title,
	DialogParameters? parameters = null)
	where TComponent : IComponent
	{
		var options = new DialogOptions
		{
			CloseButton = true,
			MaxWidth = MaxWidth.ExtraSmall,
			NoHeader = true,
			FullWidth = true
		};

		var dialog = await DialogService.ShowAsync<TComponent>(
			title,
			parameters!,
			options);

		var result = await dialog.Result;
	}

	private async Task SelecFilesToDownload()
	{
		try
		{
			var parameters = new DialogParameters
			{
				{ nameof(SelectFilesToDownloadComponent.ReportResult), ReportResult },
			
			};

			await OpenResultDialog<SelectFilesToDownloadComponent>(
				"",
				parameters);
		}
		catch (Exception)
		{
			Snackbar.Add("Failed to load ATS result details.", Severity.Error);
		}
	}

	private void CloseDialog()
	{
		MudDialog.Close();
	}

}
