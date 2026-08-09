namespace FrontendWebassembly.Component.ATS;

public partial class ATSResultComponent
{
	private MudForm? form;
	private bool IsLoaded = true;

	private string GetOrderStatusText()
		=> string.Equals(ReportResult?.OrderStatus, "Completed", StringComparison.OrdinalIgnoreCase)
			? "Completed"
			: "In progress";

	private string GetResultText()
		=> string.IsNullOrWhiteSpace(ReportResult?.HitStatus) ? "Not yet available" : ReportResult.HitStatus;

	private string GetResultValueClass()
		=> string.IsNullOrWhiteSpace(ReportResult?.HitStatus) ? "muted" : string.Empty;

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
