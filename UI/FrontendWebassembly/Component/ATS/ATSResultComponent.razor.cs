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
	public string? ReportUploadedAt { get; set; }
	public string? FilledFormAt { get; set; }
	
	private async Task ProcessBulkInvite()
	{
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

	protected override void OnParametersSet()
	{
		if (ReportResult is null)
		{
			return;
		}

		ReportResult.OrderStatus = ReportResult.OrderStatus ?? "-";
		ReportResult.HitStatus = ReportResult.HitStatus ?? "-";
		ReportResult.SelectedPackage = ReportResult.SelectedPackage ?? "-";

		ReportResult.ResumeFileName = ReportResult.ResumeFileName ?? "-";
		ReportResult.IdUploadedFileName = ReportResult.IdUploadedFileName ?? "-";
		ReportResult.CoeFileName = ReportResult.CoeFileName ?? "-";
		ReportResult.DiplomaFileName = ReportResult.DiplomaFileName ?? "-";
		ReportResult.BiometricPhotoFileName = ReportResult.BiometricPhotoFileName ?? "-";
		ReportResult.ConsentFormFileName = ReportResult.ConsentFormFileName ?? "-";
		ReportResult.UploadedReportFileName = ReportResult.UploadedReportFileName ?? "-";
		FilledFormAt = ReportResult.FilledFormAt?.ToString("MMMM dd, yyyy") ?? "-";
		ReportUploadedAt = ReportResult.ReportUploadedAt?.ToString("MMMM dd, yyyy") ?? "-";
	}
}
