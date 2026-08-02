namespace FrontendWebassembly.Component.ATS;

public partial class ATSResultComponent
{
	private MudForm? form;
	private bool IsLoaded = true;


	private bool showReportUploader = false;

	[Parameter]
	public EventCallback OnUploadSucceededReload { get; set; }

	[Parameter]
	public Guid EmailInvitationId { get; set; }

	[Parameter]
	public ATSResultDetailsDTO? ReportResult { get; set; }

	private async Task OpenResultDialog<TComponent>(
	string title,
	DialogParameters? parameters = null)
	where TComponent : IComponent
	{
		var options = new DialogOptions
		{
			CloseButton = true,
			MaxWidth = MaxWidth.ExtraSmall,
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

	private void ShowUploadReport()
	{
		showReportUploader = true;
	}

	private void GoBackToSearchReport()
	{
		showReportUploader = false;
	}

	private async Task ReloadReportResult()
	{
		ReportResult = await ReportService.GetReportResultByEmailInvitationRequestIdAsync(EmailInvitationId);
		showReportUploader = false;

		StateHasChanged();

		await OnUploadSucceededReload.InvokeAsync();
	}
}
